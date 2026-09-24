namespace ProposalGenerator.Rendering.Tests.Support;

/// <summary>Locates the repository's template folder and the test project's fixtures.</summary>
internal static class TestPaths
{
    /// <summary>
    /// Set this variable to render a different template folder, for example a checkout of a
    /// template branch that has not been merged yet. It must point to an existing directory.
    /// </summary>
    public const string TemplatesOverrideVariable = "PROPOSALGENERATOR_TEMPLATES_DIR";

    private static readonly Lazy<string> Root = new(FindRepositoryRoot);

    public static string RepositoryRoot => Root.Value;

    public static string TemplatesDirectory
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable(TemplatesOverrideVariable);
            if (string.IsNullOrWhiteSpace(overridden))
            {
                return Path.Combine(RepositoryRoot, "templates");
            }

            if (!Directory.Exists(overridden))
            {
                throw new DirectoryNotFoundException($"{TemplatesOverrideVariable} points to '{overridden}', which does not exist.");
            }

            return overridden;
        }
    }

    public static bool TemplatesOverridden =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(TemplatesOverrideVariable));

    public static string FixturesDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "templates", "README.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src", "ProposalGenerator.Rendering")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No repository root (with templates/README.md) above '{AppContext.BaseDirectory}'.");
    }
}
