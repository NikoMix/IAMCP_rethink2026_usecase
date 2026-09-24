using ProposalGenerator.Agent.Definition;
using ProposalGenerator.Agent.Deployment;

namespace ProposalGenerator.Agent.Tests;

public class AgentDeployerTests
{
    [Fact]
    public async Task First_deploy_creates_the_agent_with_exact_instructions_and_tools()
    {
        var definition = TestConfiguration.LoadRealDefinition();
        var client = new FakeFoundryAgentClient();

        var result = await new AgentDeployer(client).DeployAsync(definition);

        Assert.Equal(DeploymentOutcome.Created, result.Outcome);
        Assert.Equal("proposal-agent", result.AgentName);
        Assert.Equal("1", result.Version);
        Assert.Equal(1, client.CreateCalls);

        var spec = client.LastSpec!;
        var expectedInstructions = File.ReadAllText(Path.Combine(TestPaths.AgentDirectory, "instructions.md")).Replace("\r\n", "\n").Trim() + "\n";
        Assert.Equal(expectedInstructions, spec.Instructions);
        Assert.Equal(TestConfiguration.ModelDeployment, spec.Model.DeploymentName);
        Assert.Equal(["file_search", "function"], spec.Tools.Select(t => t.Type));
        Assert.Equal([TestConfiguration.VectorStoreId], ((FileSearchToolDefinition)spec.Tools[0]).VectorStoreIds);
        Assert.Equal("check_plausibility", ((FunctionToolDefinition)spec.Tools[1]).Name);
        Assert.Equal("proposal_agent_response", spec.ResponseFormatName);
        Assert.Equal(result.Fingerprint, spec.Metadata[AgentDeploymentSpec.FingerprintMetadataKey]);
        Assert.Equal("1.0.0", spec.Metadata[AgentDeploymentSpec.DefinitionVersionMetadataKey]);
    }

    [Fact]
    public async Task Rerun_with_the_same_definition_changes_nothing()
    {
        var client = new FakeFoundryAgentClient();
        var deployer = new AgentDeployer(client);

        var first = await deployer.DeployAsync(TestConfiguration.LoadRealDefinition());
        var second = await deployer.DeployAsync(TestConfiguration.LoadRealDefinition());

        Assert.Equal(DeploymentOutcome.Unchanged, second.Outcome);
        Assert.Equal(first.Version, second.Version);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(1, client.CreateCalls);
        Assert.Equal(1, client.AgentCount);
        Assert.Single(client.Versions("proposal-agent"));
    }

    [Fact]
    public async Task Line_endings_of_the_instructions_file_do_not_cause_an_update()
    {
        using var lf = new DefinitionWorkspace();
        using var crlf = new DefinitionWorkspace();
        var text = File.ReadAllText(Path.Combine(lf.AgentDirectory, "instructions.md")).Replace("\r\n", "\n");
        lf.Write("agent/instructions.md", text);
        crlf.Write("agent/instructions.md", text.Replace("\n", "\r\n"));
        var client = new FakeFoundryAgentClient();
        var deployer = new AgentDeployer(client);

        await deployer.DeployAsync(TestConfiguration.Loader().Load(lf.DefinitionPath));
        var second = await deployer.DeployAsync(TestConfiguration.Loader().Load(crlf.DefinitionPath));

        Assert.Equal(DeploymentOutcome.Unchanged, second.Outcome);
        Assert.Equal(1, client.CreateCalls);
    }

    [Theory]
    [InlineData("agent/instructions.md", "## Language", "## Languages")]
    [InlineData("agent/proposal-agent.yaml", "temperature: 0.2", "temperature: 0.3")]
    [InlineData("agent/tools/check_plausibility.parameters.json", "\"document\"", "\"payload\"")]
    [InlineData("schemas/sow.schema.json", "\"Statement of Work (SOW)\"", "\"Statement of Work\"")]
    public async Task Changed_definition_creates_a_new_version_of_the_same_agent(string file, string needle, string replacement)
    {
        using var workspace = new DefinitionWorkspace();
        var client = new FakeFoundryAgentClient();
        var deployer = new AgentDeployer(client);
        var first = await deployer.DeployAsync(TestConfiguration.Loader().Load(workspace.DefinitionPath));

        workspace.Replace(file, needle, replacement);
        var second = await deployer.DeployAsync(TestConfiguration.Loader().Load(workspace.DefinitionPath));

        Assert.Equal(DeploymentOutcome.Updated, second.Outcome);
        Assert.Equal("2", second.Version);
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
        Assert.Equal(1, client.AgentCount);
        Assert.Equal(2, client.Versions("proposal-agent").Count);
    }

    [Fact]
    public async Task Changed_configuration_value_creates_a_new_version()
    {
        var client = new FakeFoundryAgentClient();
        var deployer = new AgentDeployer(client);
        await deployer.DeployAsync(TestConfiguration.LoadRealDefinition());

        var other = TestConfiguration.Loader(new Dictionary<string, string>
        {
            ["FOUNDRY_MODEL_DEPLOYMENT_NAME"] = "gpt-other-deployment",
            ["FOUNDRY_VECTOR_STORE_ID"] = TestConfiguration.VectorStoreId,
        }).Load(TestPaths.DefinitionPath, TestPaths.FixtureSchemas);
        var second = await deployer.DeployAsync(other);

        Assert.Equal(DeploymentOutcome.Updated, second.Outcome);
        Assert.Equal("gpt-other-deployment", client.Versions("proposal-agent")[^1].Model);
    }

    [Fact]
    public async Task Instructions_edited_in_the_portal_are_overwritten_on_the_next_deploy()
    {
        var client = new FakeFoundryAgentClient();
        var deployer = new AgentDeployer(client);
        var definition = TestConfiguration.LoadRealDefinition();
        await deployer.DeployAsync(definition);

        client.EditLatestInstructions("proposal-agent", "You are a pirate.");
        var result = await deployer.DeployAsync(definition);

        Assert.Equal(DeploymentOutcome.Updated, result.Outcome);
        Assert.Equal(definition.Instructions, client.Versions("proposal-agent")[^1].Instructions);
    }

    [Fact]
    public void Spec_is_deterministic()
    {
        var a = AgentDeploymentSpec.From(TestConfiguration.LoadRealDefinition());
        var b = AgentDeploymentSpec.From(TestConfiguration.LoadRealDefinition());

        Assert.Equal(a.CanonicalJson, b.CanonicalJson);
        Assert.Equal(a.Fingerprint, b.Fingerprint);
        Assert.Matches("^[0-9a-f]{64}$", a.Fingerprint);
    }
}
