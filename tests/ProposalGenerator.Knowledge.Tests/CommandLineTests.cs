using ProposalGenerator.Knowledge;

namespace ProposalGenerator.Knowledge.Tests;

public sealed class CommandLineTests
{
    private const string Endpoint = "https://example.services.ai.azure.com/api/projects/demo";

    private static CommandLine? Parse(string[] args, string? environmentEndpoint, out string? error) =>
        CommandLine.Parse(args, name => name == CommandLine.EndpointVariable ? environmentEndpoint : null, out error);

    [Fact]
    public void Defaults_UseEnvironmentEndpoint()
    {
        var result = Parse([], Endpoint, out var error);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(new Uri(Endpoint), result.Endpoint);
        Assert.Equal("knowledge", result.Source);
        Assert.Equal(new IngestionOptions(), result.Ingestion);
        Assert.False(result.LocalOnly);
    }

    [Fact]
    public void Options_AreParsed_AndEndpointArgumentWinsOverEnvironment()
    {
        var result = Parse(["--endpoint", Endpoint, "--source", "kb", "--vector-store-name", "demo-store", "--prune", "--dry-run"], "https://other.example", out var error);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(new Uri(Endpoint), result.Endpoint);
        Assert.Equal("kb", result.Source);
        Assert.Equal(new IngestionOptions { VectorStoreName = "demo-store", Prune = true, DryRun = true }, result.Ingestion);
    }

    [Fact]
    public void LocalOnly_NeedsNoEndpoint()
    {
        var result = Parse(["--local-only"], null, out var error);

        Assert.Null(error);
        Assert.True(result!.LocalOnly);
        Assert.Null(result.Endpoint);
    }

    [Fact]
    public void Help_IsRecognized() => Assert.True(Parse(["--help"], null, out _)!.Help);

    [Theory]
    [InlineData(null, "No Foundry project endpoint")]
    [InlineData("http://example.services.ai.azure.com", "not an absolute https URL")]
    [InlineData("not a url", "not an absolute https URL")]
    public void InvalidEndpoint_IsRejected(string? environmentEndpoint, string expected)
    {
        Assert.Null(Parse([], environmentEndpoint, out var error));
        Assert.Contains(expected, error);
    }

    [Theory]
    [InlineData("--source")]
    [InlineData("--source", "--prune")]
    [InlineData("--vector-store-name", " ")]
    public void OptionWithoutValue_IsRejected(params string[] args)
    {
        Assert.Null(Parse(args, Endpoint, out var error));
        Assert.Contains("requires a value", error);
    }

    [Fact]
    public void UnknownArgument_IsRejected()
    {
        Assert.Null(Parse(["--force"], Endpoint, out var error));
        Assert.Contains("--force", error);
    }
}

public sealed class ReportFormatterTests
{
    [Fact]
    public void Format_ListsEntriesAndSummary()
    {
        var report = new IngestionReport("vs-1", VectorStoreCreated: true, DryRun: false,
        [
            new IngestionEntry("a.md", IngestionAction.Created, "file-1"),
            new IngestionEntry("b.md", IngestionAction.Kept, "file-2"),
        ]);

        var lines = ReportFormatter.Format(report, "store");

        Assert.Equal(
        [
            "Vector store 'store': vs-1 (created)",
            "Created   a.md  file-1",
            "Kept      b.md  file-2",
            "1 created, 0 updated, 0 unchanged, 0 removed, 1 kept.",
            "Kept files no longer exist locally; run with --prune to remove them.",
        ],
            lines);
    }

    [Fact]
    public void Format_MarksDryRun()
    {
        var report = new IngestionReport(null, VectorStoreCreated: true, DryRun: true, [new IngestionEntry("a.md", IngestionAction.Created, null)]);

        var lines = ReportFormatter.Format(report, "store");

        Assert.All(lines, l => Assert.StartsWith("[dry run] ", l));
        Assert.Equal("[dry run] Vector store 'store': (does not exist yet) (would be created)", lines[0]);
        Assert.Equal("[dry run] Created   a.md", lines[1]);
    }
}
