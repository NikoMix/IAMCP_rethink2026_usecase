namespace ProposalGenerator.Knowledge;

/// <summary>Parsed command line of the ingestion tool.</summary>
public sealed record CommandLine
{
    /// <summary>Environment variable with the Foundry project endpoint (also set by <c>azd</c>).</summary>
    public const string EndpointVariable = "AZURE_AI_PROJECT_ENDPOINT";

    /// <summary>Usage text.</summary>
    public const string Usage = """
        Uploads the knowledge files to a vector store of a Microsoft Foundry project (idempotent).

        Usage:
          dotnet run --project src/ProposalGenerator.Knowledge -- [options]

        Options:
          --endpoint <url>            Foundry project endpoint. Default: $AZURE_AI_PROJECT_ENDPOINT.
          --source <directory>        Knowledge directory. Default: knowledge
          --vector-store-name <name>  Vector store to create or update. Default: proposal-generator-knowledge
          --prune                     Remove managed files whose source file was deleted locally.
          --dry-run                   Compare with the vector store and report changes without making them.
          --local-only                List the files and content hashes without connecting to Azure.
          --help                      Show this text.

        Authentication is keyless (DefaultAzureCredential), for example after 'az login'.
        """;

    /// <summary>Project endpoint; null only with <see cref="LocalOnly"/>.</summary>
    public Uri? Endpoint { get; init; }

    /// <summary>Knowledge directory.</summary>
    public string Source { get; init; } = "knowledge";

    /// <summary>Ingestion options.</summary>
    public IngestionOptions Ingestion { get; init; } = new();

    /// <summary>Only list local files.</summary>
    public bool LocalOnly { get; init; }

    /// <summary>Show the usage text.</summary>
    public bool Help { get; init; }

    /// <summary>Parses arguments. Returns null and an error message when they are invalid.</summary>
    public static CommandLine? Parse(IReadOnlyList<string> args, Func<string, string?> environment, out string? error)
    {
        error = null;
        var result = new CommandLine();
        string? endpoint = null;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help" or "-h":
                    return result with { Help = true };
                case "--prune":
                    result = result with { Ingestion = result.Ingestion with { Prune = true } };
                    break;
                case "--dry-run":
                    result = result with { Ingestion = result.Ingestion with { DryRun = true } };
                    break;
                case "--local-only":
                    result = result with { LocalOnly = true };
                    break;
                case "--endpoint" or "--source" or "--vector-store-name":
                    if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        error = $"Option {arg} requires a value.";
                        return null;
                    }

                    var value = args[++i];
                    result = arg switch
                    {
                        "--endpoint" => result,
                        "--source" => result with { Source = value },
                        _ => result with { Ingestion = result.Ingestion with { VectorStoreName = value } },
                    };
                    endpoint = arg == "--endpoint" ? value : endpoint;
                    break;
                default:
                    error = $"Unknown argument '{arg}'.";
                    return null;
            }
        }

        if (result.LocalOnly)
        {
            return result;
        }

        endpoint ??= environment(EndpointVariable);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            error = $"No Foundry project endpoint. Pass --endpoint or set {EndpointVariable}.";
            return null;
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            error = $"The endpoint '{endpoint}' is not an absolute https URL.";
            return null;
        }

        return result with { Endpoint = uri };
    }
}
