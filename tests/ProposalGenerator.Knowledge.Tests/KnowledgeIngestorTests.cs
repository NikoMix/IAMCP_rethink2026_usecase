using ProposalGenerator.Knowledge;

namespace ProposalGenerator.Knowledge.Tests;

public sealed class KnowledgeIngestorTests
{
    private const string Store = KnowledgeIngestor.DefaultVectorStoreName;

    private static readonly IngestionOptions Defaults = new();

    private static KnowledgeFile[] Sample() =>
    [
        KnowledgeFile.FromText("rate-card.json", "{ \"roles\": [] }\n"),
        KnowledgeFile.FromText("reference-projects.md", "# Reference projects\n"),
        KnowledgeFile.FromText("services/catalogue.md", "# Service catalogue\n"),
    ];

    private static Dictionary<string, string> Managed(string path, string hash) => new()
    {
        [KnowledgeIngestor.ManagedByAttribute] = KnowledgeIngestor.ManagedByValue,
        [KnowledgeIngestor.SourcePathAttribute] = path,
        [KnowledgeIngestor.ContentHashAttribute] = hash,
    };

    [Fact]
    public async Task FirstRun_CreatesStoreAndAddsEveryFileWithAttributes()
    {
        var gateway = new FakeVectorStoreGateway();

        var report = await new KnowledgeIngestor(gateway).IngestAsync(Sample(), Defaults);

        Assert.True(report.VectorStoreCreated);
        Assert.Equal(gateway.StoresByName[Store], report.VectorStoreId);
        Assert.All(report.Entries, e => Assert.Equal(IngestionAction.Created, e.Action));
        Assert.Equal(["rate-card.json", "reference-projects.md", "services/catalogue.md"], report.Entries.Select(e => e.SourcePath));
        Assert.Equal(
            [$"create {Store}", "add rate-card.json", "add reference-projects.md", "add services_catalogue.md"],
            gateway.MutatingCalls);

        var stored = gateway.Files(report.VectorStoreId!);
        Assert.Equal(3, stored.Count);
        foreach (var file in Sample())
        {
            var match = Assert.Single(stored, s => s.Attributes[KnowledgeIngestor.SourcePathAttribute] == file.SourcePath);
            Assert.Equal(Managed(file.SourcePath, file.ContentSha256), match.Attributes);
            Assert.Equal(file.Content.ToArray(), gateway.Uploads[match.FileId]);
            Assert.Contains(report.Entries, e => e.SourcePath == file.SourcePath && e.FileId == match.FileId);
        }

        Assert.True(report.HasChanges);
    }

    [Fact]
    public async Task SecondRun_WithSameContent_ChangesNothing()
    {
        var gateway = new FakeVectorStoreGateway();
        var ingestor = new KnowledgeIngestor(gateway);
        var first = await ingestor.IngestAsync(Sample(), Defaults);
        gateway.Calls.Clear();

        var second = await ingestor.IngestAsync(Sample(), Defaults);

        Assert.Empty(gateway.MutatingCalls);
        Assert.False(second.VectorStoreCreated);
        Assert.False(second.HasChanges);
        Assert.Equal(first.VectorStoreId, second.VectorStoreId);
        Assert.All(second.Entries, e => Assert.Equal(IngestionAction.Unchanged, e.Action));
        Assert.Equal(first.Entries.Select(e => e.FileId), second.Entries.Select(e => e.FileId));
    }

    [Fact]
    public async Task ExistingStore_IsReusedNotCreated()
    {
        var gateway = new FakeVectorStoreGateway();
        var storeId = gateway.AddStore(Store);

        var report = await new KnowledgeIngestor(gateway).IngestAsync(Sample(), Defaults);

        Assert.False(report.VectorStoreCreated);
        Assert.Equal(storeId, report.VectorStoreId);
        Assert.DoesNotContain(gateway.Calls, c => c.StartsWith("create", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChangedContent_AddsNewVersionBeforeRemovingOldOne()
    {
        var gateway = new FakeVectorStoreGateway();
        var ingestor = new KnowledgeIngestor(gateway);
        var first = await ingestor.IngestAsync(Sample(), Defaults);
        var oldId = first.Entries.Single(e => e.SourcePath == "rate-card.json").FileId!;
        gateway.Calls.Clear();

        var changed = Sample().Select(f => f.SourcePath == "rate-card.json" ? KnowledgeFile.FromText(f.SourcePath, "{ \"roles\": [1] }\n") : f).ToArray();
        var report = await ingestor.IngestAsync(changed, Defaults);

        var entry = report.Entries.Single(e => e.SourcePath == "rate-card.json");
        Assert.Equal(IngestionAction.Updated, entry.Action);
        Assert.NotEqual(oldId, entry.FileId);
        Assert.Equal(["add rate-card.json", $"remove {oldId}"], gateway.MutatingCalls);
        var stored = Assert.Single(gateway.Files(report.VectorStoreId!), s => s.Attributes[KnowledgeIngestor.SourcePathAttribute] == "rate-card.json");
        Assert.Equal(entry.FileId, stored.FileId);
        Assert.Equal(changed[0].ContentSha256, stored.Attributes[KnowledgeIngestor.ContentHashAttribute]);
        Assert.Equal(2, report.Count(IngestionAction.Unchanged));
    }

    [Fact]
    public async Task FailedUpload_KeepsThePreviousVersion()
    {
        var gateway = new FakeVectorStoreGateway();
        var ingestor = new KnowledgeIngestor(gateway);
        var first = await ingestor.IngestAsync(Sample(), Defaults);
        var oldId = first.Entries.Single(e => e.SourcePath == "rate-card.json").FileId!;
        gateway.FailingUploads.Add("rate-card.json");

        var changed = Sample().Select(f => f.SourcePath == "rate-card.json" ? KnowledgeFile.FromText(f.SourcePath, "{ }\n") : f).ToArray();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ingestor.IngestAsync(changed, Defaults));

        Assert.Contains(gateway.Files(first.VectorStoreId!), s => s.FileId == oldId);
        Assert.DoesNotContain(gateway.Calls, c => c.StartsWith("remove", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LineEndingOnlyChange_IsUnchanged()
    {
        var gateway = new FakeVectorStoreGateway();
        var ingestor = new KnowledgeIngestor(gateway);
        await ingestor.IngestAsync([KnowledgeFile.FromText("a.md", "# A\nline\n")], Defaults);
        gateway.Calls.Clear();

        var report = await ingestor.IngestAsync([KnowledgeFile.FromText("a.md", "# A\r\nline\r\n")], Defaults);

        Assert.Equal(IngestionAction.Unchanged, Assert.Single(report.Entries).Action);
        Assert.Empty(gateway.MutatingCalls);
    }

    [Fact]
    public async Task FileDeletedLocally_WithoutPrune_IsKept()
    {
        var gateway = new FakeVectorStoreGateway();
        var ingestor = new KnowledgeIngestor(gateway);
        var first = await ingestor.IngestAsync(Sample(), Defaults);
        gateway.Calls.Clear();

        var report = await ingestor.IngestAsync(Sample()[..2], Defaults);

        var entry = report.Entries.Single(e => e.SourcePath == "services/catalogue.md");
        Assert.Equal(IngestionAction.Kept, entry.Action);
        Assert.Equal(first.Entries.Single(e => e.SourcePath == "services/catalogue.md").FileId, entry.FileId);
        Assert.Empty(gateway.MutatingCalls);
        Assert.Equal(3, gateway.Files(report.VectorStoreId!).Count);
        Assert.False(report.HasChanges);
    }

    [Fact]
    public async Task FileDeletedLocally_WithPrune_IsRemoved()
    {
        var gateway = new FakeVectorStoreGateway();
        var ingestor = new KnowledgeIngestor(gateway);
        var first = await ingestor.IngestAsync(Sample(), Defaults);
        var removedId = first.Entries.Single(e => e.SourcePath == "services/catalogue.md").FileId!;
        gateway.Calls.Clear();

        var report = await ingestor.IngestAsync(Sample()[..2], Defaults with { Prune = true });

        var entry = report.Entries.Single(e => e.SourcePath == "services/catalogue.md");
        Assert.Equal(IngestionAction.Removed, entry.Action);
        Assert.Null(entry.FileId);
        Assert.Equal([$"remove {removedId}"], gateway.MutatingCalls);
        Assert.Equal(2, gateway.Files(report.VectorStoreId!).Count);
        Assert.True(report.HasChanges);
    }

    [Fact]
    public async Task UnmanagedFiles_AreNeverTouched_EvenWithPrune()
    {
        var gateway = new FakeVectorStoreGateway();
        var storeId = gateway.AddStore(Store);
        var manual = gateway.AddExisting(storeId, new Dictionary<string, string>());
        var foreign = gateway.AddExisting(storeId, new Dictionary<string, string>
        {
            [KnowledgeIngestor.ManagedByAttribute] = "someone-else",
            [KnowledgeIngestor.SourcePathAttribute] = "rate-card.json",
            [KnowledgeIngestor.ContentHashAttribute] = Sample()[0].ContentSha256,
        });
        var unnamed = gateway.AddExisting(storeId, new Dictionary<string, string> { [KnowledgeIngestor.ManagedByAttribute] = KnowledgeIngestor.ManagedByValue });

        var report = await new KnowledgeIngestor(gateway).IngestAsync(Sample(), Defaults with { Prune = true });

        Assert.All(report.Entries, e => Assert.Equal(IngestionAction.Created, e.Action));
        Assert.DoesNotContain(gateway.Calls, c => c.StartsWith("remove", StringComparison.Ordinal));
        Assert.Subset(gateway.Files(storeId).Select(f => f.FileId).ToHashSet(), new HashSet<string> { manual, foreign, unnamed });
    }

    [Fact]
    public async Task DuplicatesOfUnchangedFile_AreCleanedUp()
    {
        var gateway = new FakeVectorStoreGateway();
        var storeId = gateway.AddStore(Store);
        var file = Sample()[0];
        var keep = gateway.AddExisting(storeId, Managed(file.SourcePath, file.ContentSha256));
        var duplicate = gateway.AddExisting(storeId, Managed(file.SourcePath, file.ContentSha256));
        var stale = gateway.AddExisting(storeId, Managed(file.SourcePath, new string('0', 64)));

        var report = await new KnowledgeIngestor(gateway).IngestAsync([file], Defaults);

        var entry = Assert.Single(report.Entries);
        Assert.Equal(IngestionAction.Unchanged, entry.Action);
        Assert.Equal(keep, entry.FileId);
        Assert.Equal([$"remove {duplicate}", $"remove {stale}"], gateway.MutatingCalls);
        Assert.Equal(keep, Assert.Single(gateway.Files(storeId)).FileId);
    }

    [Fact]
    public async Task DryRun_OnMissingStore_ReportsButCreatesNothing()
    {
        var gateway = new FakeVectorStoreGateway();

        var report = await new KnowledgeIngestor(gateway).IngestAsync(Sample(), Defaults with { DryRun = true });

        Assert.Empty(gateway.MutatingCalls);
        Assert.Empty(gateway.StoresByName);
        Assert.True(report.DryRun);
        Assert.True(report.VectorStoreCreated);
        Assert.Null(report.VectorStoreId);
        Assert.All(report.Entries, e => Assert.Equal((IngestionAction.Created, (string?)null), (e.Action, e.FileId)));
    }

    [Fact]
    public async Task DryRun_OnExistingStore_ReportsUpdatesAndPrunesWithoutCalls()
    {
        var gateway = new FakeVectorStoreGateway();
        var ingestor = new KnowledgeIngestor(gateway);
        await ingestor.IngestAsync(Sample(), Defaults);
        gateway.Calls.Clear();
        var before = gateway.Files(gateway.StoresByName[Store]).ToArray();

        var changed = new[] { KnowledgeFile.FromText("rate-card.json", "{ }\n"), Sample()[1], KnowledgeFile.FromText("new.md", "# New\n") };
        var report = await ingestor.IngestAsync(changed, Defaults with { DryRun = true, Prune = true });

        Assert.Empty(gateway.MutatingCalls);
        Assert.Equal(before, gateway.Files(gateway.StoresByName[Store]));
        Assert.Equal(
            [("new.md", IngestionAction.Created), ("rate-card.json", IngestionAction.Updated), ("reference-projects.md", IngestionAction.Unchanged), ("services/catalogue.md", IngestionAction.Removed)],
            report.Entries.Select(e => (e.SourcePath, e.Action)));
        Assert.True(report.HasChanges);
    }

    [Fact]
    public async Task DuplicateSourcePath_IsRejectedBeforeAnyCall()
    {
        var gateway = new FakeVectorStoreGateway();
        var files = new[] { KnowledgeFile.FromText("a.md", "1"), KnowledgeFile.FromText("a.md", "2") };

        await Assert.ThrowsAsync<ArgumentException>(() => new KnowledgeIngestor(gateway).IngestAsync(files, Defaults));

        Assert.Empty(gateway.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task EmptyVectorStoreName_IsRejected(string name)
    {
        var gateway = new FakeVectorStoreGateway();

        await Assert.ThrowsAsync<ArgumentException>(() => new KnowledgeIngestor(gateway).IngestAsync(Sample(), Defaults with { VectorStoreName = name }));

        Assert.Empty(gateway.Calls);
    }

    [Theory]
    [InlineData("rate-card.json", "rate-card.json")]
    [InlineData("services/catalogue.md", "services_catalogue.md")]
    public void UploadName_FlattensDirectories(string sourcePath, string expected) =>
        Assert.Equal(expected, KnowledgeIngestor.UploadName(sourcePath));
}
