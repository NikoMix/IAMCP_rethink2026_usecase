using ProposalGenerator.Agent.Definition;

namespace ProposalGenerator.Agent.Tests;

public class AgentDefinitionLoaderTests
{
    [Fact]
    public void Repository_definition_loads_with_resolved_configuration()
    {
        var definition = TestConfiguration.LoadRealDefinition();

        Assert.Equal("proposal-agent", definition.Name);
        Assert.Equal("1.0.0", definition.Version);
        Assert.False(string.IsNullOrWhiteSpace(definition.Description));
        Assert.Equal(TestConfiguration.ModelDeployment, definition.Model.DeploymentName);
        Assert.StartsWith("# Proposal agent instructions", definition.Instructions);
        Assert.DoesNotContain("\r", definition.Instructions);

        Assert.Equal(2, definition.Tools.Count);
        var fileSearch = Assert.IsType<FileSearchToolDefinition>(definition.Tools[0]);
        Assert.Equal([TestConfiguration.VectorStoreId], fileSearch.VectorStoreIds);
        var plausibility = Assert.IsType<FunctionToolDefinition>(definition.Tools[1]);
        Assert.Equal("check_plausibility", plausibility.Name);
        Assert.Equal(["documentType", "document"], plausibility.Parameters["required"]!.AsArray().Select(n => n!.GetValue<string>()));

        Assert.Equal("proposal_agent_response", definition.ResponseFormat.Name);
        Assert.False(definition.ResponseFormat.Strict);
        Assert.Equal(["rfi", "rfp", "msa", "sow", "change-request"], definition.ResponseFormat.Schemas.DocumentTypes);
    }

    [Theory]
    [InlineData("Legal boundaries", "qualified legal counsel")]
    [InlineData("Never invent values", "Never invent prices")]
    [InlineData("Collecting data", "Ask at most three questions per message")]
    [InlineData("Language", "German or English")]
    [InlineData("Never invent values", "check_plausibility")]
    public void Instructions_cover_the_mandatory_rules(string section, string rule)
    {
        var instructions = TestConfiguration.LoadRealDefinition().Instructions;

        Assert.Contains($"\n## {section}\n", instructions);
        Assert.Contains(rule, instructions);
    }

    [Fact]
    public void Instructions_name_every_configured_document_type()
    {
        var definition = TestConfiguration.LoadRealDefinition();

        foreach (var type in definition.ResponseFormat.Schemas.DocumentTypes)
        {
            Assert.Contains($"(`{type}`)", definition.Instructions);
        }
    }

    [Fact]
    public void Missing_configuration_value_fails_with_its_name()
    {
        var loader = TestConfiguration.Loader(new Dictionary<string, string>
        {
            ["FOUNDRY_VECTOR_STORE_ID"] = TestConfiguration.VectorStoreId,
        });

        var ex = Assert.Throws<AgentDefinitionException>(() => loader.Load(TestPaths.DefinitionPath, TestPaths.FixtureSchemas));

        Assert.Contains(ex.Errors, e => e.Contains("model.deployment") && e.Contains("FOUNDRY_MODEL_DEPLOYMENT_NAME"));
    }

    [Fact]
    public void Missing_definition_file_fails()
    {
        var ex = Assert.Throws<AgentDefinitionException>(
            () => TestConfiguration.Loader().Load(Path.Combine(Path.GetTempPath(), "does-not-exist.yaml")));

        Assert.Contains("does not exist", Assert.Single(ex.Errors));
    }

    [Fact]
    public void Unknown_key_fails_instead_of_being_ignored()
    {
        using var workspace = new DefinitionWorkspace();
        workspace.Replace("agent/proposal-agent.yaml", "temperature: 0.2", "temprature: 0.2");

        var ex = Assert.Throws<AgentDefinitionException>(() => TestConfiguration.Loader().Load(workspace.DefinitionPath));

        Assert.Contains("temprature", Assert.Single(ex.Errors));
    }

    [Fact]
    public void Malformed_yaml_fails_with_location()
    {
        using var workspace = new DefinitionWorkspace();
        workspace.Replace("agent/proposal-agent.yaml", "name: proposal-agent", "name: [proposal-agent");

        var ex = Assert.Throws<AgentDefinitionException>(() => TestConfiguration.Loader().Load(workspace.DefinitionPath));

        Assert.Contains("YAML error at line", Assert.Single(ex.Errors));
    }

    [Theory]
    [InlineData("name: proposal-agent", "name: proposal_agent!", "name 'proposal_agent!'")]
    [InlineData("version: 1.0.0", "version: latest", "version 'latest'")]
    [InlineData("schemaVersion: 1", "schemaVersion: 2", "schemaVersion must be 1")]
    [InlineData("instructions: instructions.md", "instructions: missing.md", "missing.md' does not exist")]
    [InlineData("instructions: instructions.md", "instructions: /etc/instructions.md", "must be a path relative")]
    [InlineData("  - type: file_search", "  - type: code_interpreter", "tools[0].type 'code_interpreter' is not supported")]
    [InlineData("parameters: tools/check_plausibility.parameters.json", "parameters: tools/missing.json", "missing.json' does not exist")]
    [InlineData("    name: check_plausibility", "    name: check plausibility", "tools[1].name 'check plausibility'")]
    [InlineData("  type: json_schema", "  type: text", "responseFormat.type must be json_schema")]
    [InlineData("    - msa\n", "    - msa\n    - nda\n", "No schema file 'nda.schema.json'")]
    [InlineData("    - msa\n", "    - msa\n    - msa\n", "lists 'msa' more than once")]
    [InlineData("deployment: ${FOUNDRY_MODEL_DEPLOYMENT_NAME}", "deployment: prefix-${FOUNDRY_MODEL_DEPLOYMENT_NAME}", "exactly one ${NAME} reference")]
    [InlineData("temperature: 0.2", "temperature: 3", "model.temperature must be between 0 and 2")]
    [InlineData("schemaDirectory: ../schemas", "schemaDirectory: ../nowhere", "does not exist")]
    public void Invalid_definition_fails_loudly(string needle, string replacement, string expectedError)
    {
        using var workspace = new DefinitionWorkspace();
        workspace.Replace("agent/proposal-agent.yaml", needle, replacement);

        var ex = Assert.Throws<AgentDefinitionException>(() => TestConfiguration.Loader().Load(workspace.DefinitionPath));

        Assert.Contains(ex.Errors, e => e.Contains(expectedError, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        using var workspace = new DefinitionWorkspace();
        workspace.Replace("agent/proposal-agent.yaml", "name: proposal-agent", "name: -bad-");
        workspace.Replace("agent/proposal-agent.yaml", "instructions: instructions.md", "instructions: missing.md");

        var ex = Assert.Throws<AgentDefinitionException>(
            () => TestConfiguration.Loader(new Dictionary<string, string>()).Load(workspace.DefinitionPath));

        Assert.Contains(ex.Errors, e => e.StartsWith("name '-bad-'", StringComparison.Ordinal));
        Assert.Contains(ex.Errors, e => e.Contains("missing.md"));
        Assert.Contains(ex.Errors, e => e.Contains("FOUNDRY_MODEL_DEPLOYMENT_NAME"));
        Assert.Contains(ex.Errors, e => e.Contains("FOUNDRY_VECTOR_STORE_ID"));
    }

    [Fact]
    public void Empty_instructions_fail()
    {
        using var workspace = new DefinitionWorkspace();
        workspace.Write("agent/instructions.md", "  \n\n");

        var ex = Assert.Throws<AgentDefinitionException>(() => TestConfiguration.Loader().Load(workspace.DefinitionPath));

        Assert.Contains(ex.Errors, e => e.Contains("is empty"));
    }

    [Fact]
    public void Function_parameters_must_be_json()
    {
        using var workspace = new DefinitionWorkspace();
        workspace.Write("agent/tools/check_plausibility.parameters.json", "{ not json");

        var ex = Assert.Throws<AgentDefinitionException>(() => TestConfiguration.Loader().Load(workspace.DefinitionPath));

        Assert.Contains(ex.Errors, e => e.Contains("tools[1].parameters") && e.Contains("not a valid JSON Schema"));
    }

    [Fact]
    public void Unresolvable_cross_file_reference_fails()
    {
        using var workspace = new DefinitionWorkspace();
        workspace.Replace("schemas/sow.schema.json", "common.schema.json#/$defs/party", "common.schema.json#/$defs/nope");

        var ex = Assert.Throws<AgentDefinitionException>(() => TestConfiguration.Loader().Load(workspace.DefinitionPath));

        Assert.Contains(ex.Errors, e => e.Contains("Unresolved $ref") && e.Contains("/$defs/nope"));
    }

    [Theory]
    [InlineData("schemas/sow.schema.json", "\"title\"", "\"description\": \"duplicate\", \"title\"", "sow.schema.json")]
    [InlineData("schemas/common.schema.json", "\"title\": { \"$ref\": \"#/$defs/text\" }", "\"title\": { \"$ref\": \"#/$defs/text\", \"$ref\": \"#/$defs/text\" }", "common.schema.json")]
    [InlineData("agent/tools/check_plausibility.parameters.json", "\"type\": \"object\"", "\"type\": \"object\", \"type\": \"object\"", "tools[1].parameters")]
    public void Duplicate_json_property_fails(string file, string needle, string replacement, string expectedLocation)
    {
        using var workspace = new DefinitionWorkspace();
        workspace.Replace(file, needle, replacement);

        var ex = Assert.Throws<AgentDefinitionException>(() => TestConfiguration.Loader().Load(workspace.DefinitionPath));

        Assert.Contains(ex.Errors, e => e.Contains(expectedLocation) && e.Contains("duplicate property", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Schema_that_is_not_a_json_schema_fails()
    {
        using var workspace = new DefinitionWorkspace();
        workspace.Write("schemas/msa.schema.json", """{ "type": "objekt" }""");

        var ex = Assert.Throws<AgentDefinitionException>(() => TestConfiguration.Loader().Load(workspace.DefinitionPath));

        Assert.Contains(ex.Errors, e => e.Contains("msa.schema.json") && e.Contains("not a valid JSON Schema"));
    }
}
