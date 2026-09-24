using System.Text.Json.Nodes;
using ProposalGenerator.Agent.Definition;

namespace ProposalGenerator.Agent.Tests;

internal static class TestPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string AgentDirectory => Path.Combine(RepositoryRoot, "agent");

    public static string DefinitionPath => Path.Combine(AgentDirectory, "proposal-agent.yaml");

    public static string FixtureSchemas => Path.Combine(AppContext.BaseDirectory, "Fixtures", "schemas");

    public static JsonObject ValidSow() =>
        (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sow.valid.json")))!;

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "agent", "proposal-agent.yaml")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root with agent/proposal-agent.yaml not found.");
    }
}

internal static class TestConfiguration
{
    public const string ModelDeployment = "gpt-test-deployment";
    public const string VectorStoreId = "vs_test_0001";

    public static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>
    {
        ["FOUNDRY_MODEL_DEPLOYMENT_NAME"] = ModelDeployment,
        ["FOUNDRY_VECTOR_STORE_ID"] = VectorStoreId,
    };

    public static AgentDefinitionLoader Loader(IReadOnlyDictionary<string, string>? values = null)
    {
        var source = values ?? Values;
        return new AgentDefinitionLoader(name => source.TryGetValue(name, out var value) ? value : null);
    }

    public static AgentDefinition LoadRealDefinition() =>
        Loader().Load(TestPaths.DefinitionPath, TestPaths.FixtureSchemas);
}

/// <summary>Temporary copy of agent/ with the fixture schemas at ../schemas, for mutation tests.</summary>
internal sealed class DefinitionWorkspace : IDisposable
{
    public DefinitionWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "pg-agent-tests", Guid.NewGuid().ToString("N"));
        AgentDirectory = Path.Combine(Root, "agent");
        SchemaDirectory = Path.Combine(Root, "schemas");
        Copy(TestPaths.AgentDirectory, AgentDirectory);
        Copy(TestPaths.FixtureSchemas, SchemaDirectory);
    }

    public string Root { get; }
    public string AgentDirectory { get; }
    public string SchemaDirectory { get; }
    public string DefinitionPath => Path.Combine(AgentDirectory, "proposal-agent.yaml");

    /// <summary>Replaces text in a workspace file and fails if the needle is absent, so a mutation is never a silent no-op.</summary>
    public void Replace(string relativePath, string needle, string replacement)
    {
        var path = Path.Combine(Root, relativePath);
        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        Assert.Contains(needle, text);
        File.WriteAllText(path, text.Replace(needle, replacement));
    }

    public void Write(string relativePath, string content) => File.WriteAllText(Path.Combine(Root, relativePath), content);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort; the directory is under the temp path.
        }
    }

    private static void Copy(string source, string target)
    {
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }
}
