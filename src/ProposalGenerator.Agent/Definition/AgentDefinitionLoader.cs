using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using ProposalGenerator.Agent.Schemas;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ProposalGenerator.Agent.Definition;

/// <summary>Loads <c>agent/proposal-agent.yaml</c>, resolves configuration references, and validates the result.</summary>
public sealed partial class AgentDefinitionLoader
{
    public const int SupportedSchemaVersion = 1;

    private readonly Func<string, string?> _configuration;

    /// <param name="configuration">Resolves <c>${NAME}</c> references, for example from environment variables.</param>
    public AgentDefinitionLoader(Func<string, string?> configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <param name="definitionPath">Path of the YAML definition. Relative paths inside it resolve against its directory.</param>
    /// <param name="schemaDirectoryOverride">Optional schema directory that replaces <c>responseFormat.schemaDirectory</c>.</param>
    /// <exception cref="AgentDefinitionException">The definition is missing, malformed, or invalid.</exception>
    public AgentDefinition Load(string definitionPath, string? schemaDirectoryOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionPath);
        var fullPath = Path.GetFullPath(definitionPath);
        var errors = new List<string>();

        var document = Parse(fullPath);
        var baseDirectory = Path.GetDirectoryName(fullPath)!;

        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            errors.Add($"schemaVersion must be {SupportedSchemaVersion}.");
        }

        var name = Require(document.Name, "name", errors);
        if (name is not null && !AgentNamePattern().IsMatch(name))
        {
            errors.Add($"name '{name}' must be 1-63 letters, digits, or hyphens, and start and end with a letter or digit.");
        }

        var version = Require(document.Version, "version", errors);
        if (version is not null && !VersionPattern().IsMatch(version))
        {
            errors.Add($"version '{version}' must have the form MAJOR.MINOR.PATCH.");
        }

        var description = Require(document.Description, "description", errors)?.Trim();
        if (description is { Length: > 512 })
        {
            errors.Add("description must not exceed 512 characters.");
        }

        var model = LoadModel(document.Model, errors);
        var instructions = LoadInstructions(document.Instructions, baseDirectory, errors);
        var tools = LoadTools(document.Tools, baseDirectory, errors);
        var responseFormat = LoadResponseFormat(document.ResponseFormat, baseDirectory, schemaDirectoryOverride, errors);

        if (errors.Count > 0)
        {
            throw new AgentDefinitionException(fullPath, errors);
        }

        return new AgentDefinition
        {
            SourcePath = fullPath,
            Name = name!,
            Version = version!,
            Description = description!,
            Model = model!,
            Instructions = instructions!,
            Tools = tools,
            ResponseFormat = responseFormat!,
        };
    }

    private static AgentDefinitionDocument Parse(string path)
    {
        if (!File.Exists(path))
        {
            throw new AgentDefinitionException(path, ["The definition file does not exist."]);
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        try
        {
            return deserializer.Deserialize<AgentDefinitionDocument?>(File.ReadAllText(path))
                ?? throw new AgentDefinitionException(path, ["The definition file is empty."]);
        }
        catch (YamlException ex)
        {
            var detail = ex.InnerException is null ? ex.Message : $"{ex.Message} {ex.InnerException.Message}";
            throw new AgentDefinitionException(path, [$"YAML error at line {ex.Start.Line}, column {ex.Start.Column}: {detail}"]);
        }
    }

    private AgentModelSettings? LoadModel(ModelDocument? model, List<string> errors)
    {
        if (model is null)
        {
            errors.Add("model is required.");
            return null;
        }

        var deployment = Resolve(Require(model.Deployment, "model.deployment", errors), "model.deployment", errors);

        if (model.Temperature is < 0 or > 2)
        {
            errors.Add("model.temperature must be between 0 and 2.");
        }

        if (model.TopP is <= 0 or > 1)
        {
            errors.Add("model.topP must be greater than 0 and at most 1.");
        }

        return deployment is null ? null : new AgentModelSettings(deployment, model.Temperature, model.TopP);
    }

    private static string? LoadInstructions(string? relativePath, string baseDirectory, List<string> errors)
    {
        var path = ResolvePath(Require(relativePath, "instructions", errors), baseDirectory, "instructions", errors);
        if (path is null)
        {
            return null;
        }

        if (!File.Exists(path))
        {
            errors.Add($"instructions file '{path}' does not exist.");
            return null;
        }

        // Normalise line endings so that the deployed text and its fingerprint do not depend on git checkout settings.
        var text = File.ReadAllText(path).Replace("\r\n", "\n").Trim();
        if (text.Length == 0)
        {
            errors.Add($"instructions file '{path}' is empty.");
            return null;
        }

        return text + "\n";
    }

    private List<AgentToolDefinition> LoadTools(List<ToolDocument>? tools, string baseDirectory, List<string> errors)
    {
        var result = new List<AgentToolDefinition>();
        if (tools is null)
        {
            return result;
        }

        for (var i = 0; i < tools.Count; i++)
        {
            var tool = tools[i];
            var at = $"tools[{i}]";
            switch (tool?.Type)
            {
                case "file_search":
                    RejectKeys(at, "file_search", errors, (tool.Name, "name"), (tool.Description, "description"), (tool.Parameters, "parameters"), (tool.Strict, "strict"));
                    var ids = new List<string>();
                    if (tool.VectorStoreIds is not { Count: > 0 })
                    {
                        errors.Add($"{at}.vectorStoreIds must list at least one vector store ID.");
                    }
                    else
                    {
                        for (var j = 0; j < tool.VectorStoreIds.Count; j++)
                        {
                            var id = Resolve(Require(tool.VectorStoreIds[j], $"{at}.vectorStoreIds[{j}]", errors), $"{at}.vectorStoreIds[{j}]", errors);
                            if (id is not null)
                            {
                                ids.Add(id);
                            }
                        }
                    }

                    if (tool.MaxResults is < 1 or > 50)
                    {
                        errors.Add($"{at}.maxResults must be between 1 and 50.");
                    }

                    result.Add(new FileSearchToolDefinition { VectorStoreIds = ids, MaxResults = tool.MaxResults });
                    break;

                case "function":
                    RejectKeys(at, "function", errors, (tool.VectorStoreIds, "vectorStoreIds"), (tool.MaxResults, "maxResults"));
                    var name = Require(tool.Name, $"{at}.name", errors);
                    if (name is not null && !FunctionNamePattern().IsMatch(name))
                    {
                        errors.Add($"{at}.name '{name}' must be 1-64 letters, digits, underscores, or hyphens.");
                    }
                    else if (name is not null && result.OfType<FunctionToolDefinition>().Any(f => f.Name == name))
                    {
                        errors.Add($"{at}.name '{name}' is declared more than once.");
                    }

                    var description = Require(tool.Description, $"{at}.description", errors)?.Trim();
                    var parameters = LoadParameters(tool.Parameters, baseDirectory, $"{at}.parameters", errors);
                    if (name is not null && description is not null && parameters is not null)
                    {
                        result.Add(new FunctionToolDefinition
                        {
                            Name = name,
                            Description = description,
                            Parameters = parameters,
                            Strict = tool.Strict ?? false,
                        });
                    }

                    break;

                case null:
                    errors.Add($"{at}.type is required.");
                    break;

                default:
                    errors.Add($"{at}.type '{tool.Type}' is not supported; use file_search or function.");
                    break;
            }
        }

        return result;
    }

    private static JsonObject? LoadParameters(string? relativePath, string baseDirectory, string at, List<string> errors)
    {
        var path = ResolvePath(Require(relativePath, at, errors), baseDirectory, at, errors);
        if (path is null)
        {
            return null;
        }

        if (!File.Exists(path))
        {
            errors.Add($"{at} file '{path}' does not exist.");
            return null;
        }

        try
        {
            if (StrictJson.Parse(File.ReadAllText(path)) is not JsonObject parameters)
            {
                errors.Add($"{at} file '{path}' must contain a JSON object.");
                return null;
            }

            JsonSchema.FromText(parameters.ToJsonString(), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
            if (parameters["type"]?.GetValue<string>() != "object")
            {
                errors.Add($"{at} schema '{path}' must have type 'object'.");
                return null;
            }

            parameters.Remove("$schema");
            return parameters;
        }
        catch (Exception ex) when (ex is JsonException or JsonSchemaException or InvalidOperationException)
        {
            errors.Add($"{at} file '{path}' is not a valid JSON Schema: {ex.Message}");
            return null;
        }
    }

    private static ResponseFormatDefinition? LoadResponseFormat(
        ResponseFormatDocument? format,
        string baseDirectory,
        string? schemaDirectoryOverride,
        List<string> errors)
    {
        if (format is null)
        {
            errors.Add("responseFormat is required.");
            return null;
        }

        if (format.Type != "json_schema")
        {
            errors.Add("responseFormat.type must be json_schema.");
        }

        var name = Require(format.Name, "responseFormat.name", errors);
        if (name is not null && !FunctionNamePattern().IsMatch(name))
        {
            errors.Add($"responseFormat.name '{name}' must be 1-64 letters, digits, underscores, or hyphens.");
        }

        var envelope = ResolvePath(Require(format.Envelope, "responseFormat.envelope", errors), baseDirectory, "responseFormat.envelope", errors);
        var schemaDirectory = schemaDirectoryOverride is not null
            ? Path.GetFullPath(schemaDirectoryOverride)
            : ResolvePath(Require(format.SchemaDirectory, "responseFormat.schemaDirectory", errors), baseDirectory, "responseFormat.schemaDirectory", errors);

        var documentTypes = format.DocumentTypes ?? [];
        if (documentTypes.Count == 0)
        {
            errors.Add("responseFormat.documentTypes must list at least one document type.");
        }

        foreach (var type in documentTypes)
        {
            if (!DocumentTypePattern().IsMatch(type ?? string.Empty))
            {
                errors.Add($"responseFormat.documentTypes entry '{type}' must be lowercase letters and hyphens.");
            }
        }

        foreach (var duplicate in documentTypes.GroupBy(t => t).Where(g => g.Count() > 1))
        {
            errors.Add($"responseFormat.documentTypes lists '{duplicate.Key}' more than once.");
        }

        if (errors.Count > 0 || name is null || envelope is null || schemaDirectory is null)
        {
            return null;
        }

        var schemas = DocumentSchemaSet.TryLoad(envelope, schemaDirectory, documentTypes, errors);
        if (schemas is null)
        {
            return null;
        }

        var modelSchema = ModelSchemaBuilder.TryBuild(schemas.EnvelopeSource, schemas.Files, schemas.DocumentTypes, errors);
        if (modelSchema is null)
        {
            return null;
        }

        return new ResponseFormatDefinition
        {
            Name = name,
            Strict = format.Strict ?? false,
            Schemas = schemas,
            ModelSchema = modelSchema,
        };
    }

    private string? Resolve(string? value, string at, List<string> errors)
    {
        if (value is null)
        {
            return null;
        }

        var match = PlaceholderPattern().Match(value);
        if (!match.Success)
        {
            if (value.Contains("${", StringComparison.Ordinal))
            {
                errors.Add($"{at} '{value}' must be either a literal or exactly one ${{NAME}} reference.");
                return null;
            }

            return value;
        }

        var variable = match.Groups["name"].Value;
        var resolved = _configuration(variable);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            errors.Add($"{at} references configuration value '{variable}', which is not set.");
            return null;
        }

        return resolved.Trim();
    }

    private static string? Require(string? value, string at, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{at} is required.");
            return null;
        }

        return value;
    }

    private static string? ResolvePath(string? relativePath, string baseDirectory, string at, List<string> errors)
    {
        if (relativePath is null)
        {
            return null;
        }

        if (Path.IsPathRooted(relativePath))
        {
            errors.Add($"{at} '{relativePath}' must be a path relative to the definition file.");
            return null;
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
    }

    private static void RejectKeys(string at, string type, List<string> errors, params (object? Value, string Key)[] keys)
    {
        foreach (var (value, key) in keys)
        {
            if (value is not null)
            {
                errors.Add($"{at}.{key} is not allowed for {type} tools.");
            }
        }
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$")]
    private static partial Regex AgentNamePattern();

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex VersionPattern();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex FunctionNamePattern();

    [GeneratedRegex("^[a-z][a-z-]*$")]
    private static partial Regex DocumentTypePattern();

    [GeneratedRegex(@"^\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}$")]
    private static partial Regex PlaceholderPattern();
}
