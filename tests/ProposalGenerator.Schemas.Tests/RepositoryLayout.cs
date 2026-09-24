namespace ProposalGenerator.Schemas.Tests;

/// <summary>Locates the repository folders the tests read from.</summary>
internal static class RepositoryLayout
{
    private static readonly Lazy<string> Root = new(FindRoot);

    public static string RootDirectory => Root.Value;

    public static string SchemasDirectory => Path.Combine(RootDirectory, "schemas");

    public static string ValidExamplesDirectory => Path.Combine(SchemasDirectory, "examples", "valid");

    public static string InvalidExamplesDirectory => Path.Combine(SchemasDirectory, "examples", "invalid");

    public static string TemplatesDirectory => Path.Combine(RootDirectory, "templates");

    /// <summary>The five document types; every schema, example set and template must cover exactly these.</summary>
    public static IReadOnlyList<string> DocumentTypes { get; } = ["rfi", "rfp", "msa", "sow", "change-request"];

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "schemas"))
                && Directory.Exists(Path.Combine(directory.FullName, "templates")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"No ancestor of '{AppContext.BaseDirectory}' contains both 'schemas' and 'templates'.");
    }
}
