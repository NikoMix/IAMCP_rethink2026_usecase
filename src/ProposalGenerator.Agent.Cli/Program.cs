using Azure.Identity;
using ProposalGenerator.Agent.Definition;
using ProposalGenerator.Agent.Deployment;

const string Usage = """
    Usage: proposal-agent deploy [--definition <path>] [--schemas <directory>] [--dry-run]

      --definition  Agent definition YAML (default: agent/proposal-agent.yaml)
      --schemas     Schema directory; overrides responseFormat.schemaDirectory
      --dry-run     Validate and print the desired state without calling Azure

    Environment:
      FOUNDRY_PROJECT_ENDPOINT        Foundry project endpoint (not needed for --dry-run)
      FOUNDRY_MODEL_DEPLOYMENT_NAME   Model deployment referenced by the definition
      FOUNDRY_VECTOR_STORE_ID         Vector store for file search referenced by the definition

    Authentication uses DefaultAzureCredential (for example az login or a managed identity).
    """;

if (args.Length == 0 || args[0] != "deploy")
{
    Console.Error.WriteLine(Usage);
    return 2;
}

var definitionPath = Path.Combine("agent", "proposal-agent.yaml");
string? schemaDirectory = null;
var dryRun = false;

for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--definition" when i + 1 < args.Length:
            definitionPath = args[++i];
            break;
        case "--schemas" when i + 1 < args.Length:
            schemaDirectory = args[++i];
            break;
        case "--dry-run":
            dryRun = true;
            break;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument '{args[i]}'.");
            Console.Error.WriteLine(Usage);
            return 2;
    }
}

AgentDefinition definition;
try
{
    definition = new AgentDefinitionLoader(Environment.GetEnvironmentVariable).Load(definitionPath, schemaDirectory);
}
catch (AgentDefinitionException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

var spec = AgentDeploymentSpec.From(definition);
Console.WriteLine($"Agent '{spec.Name}' definition {definition.Version}, fingerprint {spec.Fingerprint}");

if (dryRun)
{
    Console.WriteLine(spec.CanonicalJson);
    return 0;
}

var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT");
if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) || endpointUri.Scheme != Uri.UriSchemeHttps)
{
    Console.Error.WriteLine("FOUNDRY_PROJECT_ENDPOINT must be set to the https endpoint of the Foundry project.");
    return 2;
}

try
{
    var deployer = new AgentDeployer(new FoundryAgentClient(endpointUri, new DefaultAzureCredential()));
    var result = await deployer.DeployAsync(definition);
    Console.WriteLine($"{result.Outcome}: agent '{result.AgentName}' version {result.Version}");
    return 0;
}
catch (Exception ex) when (ex is System.ClientModel.ClientResultException or Azure.Identity.AuthenticationFailedException or Azure.RequestFailedException)
{
    Console.Error.WriteLine($"Deployment failed: {ex.Message}");
    return 3;
}
