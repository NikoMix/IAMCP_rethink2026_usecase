using System.Text;
using System.Text.Json;
using ProposalGenerator.Knowledge;

namespace ProposalGenerator.Knowledge.Tests;

public sealed class KnowledgeFileTests
{
    [Fact]
    public void Hash_IsLowerCaseHexSha256OfContent() =>
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", KnowledgeFile.FromText("a.md", "abc").ContentSha256);

    [Theory]
    [InlineData("a\r\nb\r\n")]
    [InlineData("a\rb\r")]
    [InlineData("a\nb\n")]
    public void LineEndings_AreNormalizedToLf(string text)
    {
        var file = KnowledgeFile.FromText("a.md", text);

        Assert.Equal("a\nb\n", Encoding.UTF8.GetString(file.Content.Span));
        Assert.Equal(KnowledgeFile.FromText("a.md", "a\nb\n").ContentSha256, file.ContentSha256);
    }

    [Fact]
    public void DifferentContent_HasDifferentHash() =>
        Assert.NotEqual(KnowledgeFile.FromText("a.md", "a\n").ContentSha256, KnowledgeFile.FromText("a.md", "a \n").ContentSha256);
}

public sealed class KnowledgeSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pg-knowledge-tests-" + Guid.NewGuid().ToString("N"));

    public KnowledgeSourceTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string relative, string text = "x")
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public async Task Discover_ReturnsSupportedFilesSortedWithForwardSlashes()
    {
        Write("b.json", "{}");
        Write("a.md");
        Write("sub/c.MD");
        Write("README.md");
        Write("sub/readme.json");
        Write(".gitattributes");
        Write(".hidden/d.md");
        Write("sub/.draft.md");
        Write("notes.txt");

        var files = await KnowledgeSource.DiscoverAsync(_root);

        Assert.Equal(["a.md", "b.json", "sub/c.MD"], files.Select(f => f.SourcePath));
    }

    [Fact]
    public async Task Discover_MissingDirectory_Throws() =>
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => KnowledgeSource.DiscoverAsync(Path.Combine(_root, "missing")));

    [Fact]
    public async Task Discover_DirectoryWithoutSupportedFiles_Throws()
    {
        Write("README.md");
        Write("notes.txt");

        await Assert.ThrowsAsync<InvalidDataException>(() => KnowledgeSource.DiscoverAsync(_root));
    }

    [Theory]
    [InlineData("rate-card.json", true)]
    [InlineData("guides/onboarding.md", true)]
    [InlineData("README.md", false)]
    [InlineData("guides/Readme.md", false)]
    [InlineData(".github/x.md", false)]
    [InlineData("image.png", false)]
    public void IsIngested(string path, bool expected) => Assert.Equal(expected, KnowledgeSource.IsIngested(path));
}

/// <summary>Checks the sample content shipped in <c>knowledge/</c>.</summary>
public sealed class ShippedKnowledgeTests
{
    private static string KnowledgeDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "knowledge");
            if (File.Exists(Path.Combine(candidate, "rate-card.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("knowledge/ not found above the test output directory.");
    }

    [Fact]
    public async Task ShippedKnowledge_IsDiscoveredWithoutReadme()
    {
        var files = await KnowledgeSource.DiscoverAsync(KnowledgeDirectory());

        Assert.Equal(["rate-card.json", "reference-projects.md", "service-catalogue.md"], files.Select(f => f.SourcePath));
        Assert.True(File.Exists(Path.Combine(KnowledgeDirectory(), "README.md")), "knowledge/README.md documents the content.");
    }

    [Fact]
    public async Task ShippedKnowledge_IsLabelledFictional()
    {
        foreach (var file in await KnowledgeSource.DiscoverAsync(KnowledgeDirectory()))
        {
            var text = Encoding.UTF8.GetString(file.Content.Span);
            Assert.True(text.Contains("fictional", StringComparison.OrdinalIgnoreCase), $"{file.SourcePath} is not labelled as fictional.");
        }
    }

    [Fact]
    public async Task ShippedRateCard_IsValidJsonInEuroWithValidity()
    {
        var file = (await KnowledgeSource.DiscoverAsync(KnowledgeDirectory())).Single(f => f.SourcePath == "rate-card.json");
        using var json = JsonDocument.Parse(file.Content);
        var root = json.RootElement;

        Assert.Equal("EUR", root.GetProperty("currency").GetString());
        var from = DateOnly.Parse(root.GetProperty("valid_from").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var until = DateOnly.Parse(root.GetProperty("valid_to").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(from < until);
        var roles = root.GetProperty("roles").EnumerateArray().ToArray();
        Assert.NotEmpty(roles);
        Assert.All(roles, r => Assert.True(r.GetProperty("daily_rate").GetDecimal() > 0));
    }
}
