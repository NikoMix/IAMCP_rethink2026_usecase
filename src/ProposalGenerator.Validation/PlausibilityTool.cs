using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProposalGenerator.Validation;

/// <summary>
/// The <c>check_plausibility</c> function tool. The agent host registers <see cref="Name"/>, <see cref="Description"/>
/// and <see cref="ParametersSchema"/> as a function tool, calls <see cref="HandleAsync"/> with the arguments of each
/// function call, and returns <see cref="SerializeResult"/> as the function output.
/// </summary>
public sealed class PlausibilityTool
{
    /// <summary>Tool name as registered with the agent.</summary>
    public const string ToolName = "check_plausibility";

    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly PlausibilityChecker _checker;

    /// <summary>Creates the tool.</summary>
    /// <exception cref="ArgumentException">A configured field path is malformed.</exception>
    public PlausibilityTool(PlausibilityCheckerOptions? options = null) => _checker = new PlausibilityChecker(options);

    /// <summary>Tool name as registered with the agent (<c>check_plausibility</c>).</summary>
    public string Name => ToolName;

    /// <summary>Tool description for the model.</summary>
    public string Description =>
        "Prüft ein vollständig erfasstes Dokument (SOW, RFI, RFP, MSA oder Change Request) deterministisch auf Plausibilität: " +
        "Summen von Zahlungsplan und Rate Card, Zeiträume und Fristen, einheitliche Währung, Tagessätze gegen die gültige Rate Card, " +
        "Bezüge auf Vorgängerdokumente und negative Werte. Vor jeder Generierung aufrufen. Liefert findings (code, severity, path, message) " +
        "und computed (berechnete Werte je JSONPath), die statt eigener Berechnungen zu verwenden sind.";

    /// <summary>JSON Schema (2020-12) of the tool parameters.</summary>
    public JsonElement ParametersSchema { get; } = BuildParametersSchema();

    /// <summary>
    /// Handles a function call. Accepts the arguments as a JSON object or as the JSON string a model emits.
    /// Invalid arguments are reported as findings; the handler does not throw for them.
    /// </summary>
    /// <param name="arguments"><c>{ "documentType": "sow|rfi|rfp|msa|change-request", "document": { ... } }</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PlausibilityResult> HandleAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (arguments.ValueKind == JsonValueKind.String)
        {
            JsonDocument parsed;
            try
            {
                parsed = JsonDocument.Parse(arguments.GetString()!);
            }
            catch (JsonException)
            {
                return Task.FromResult(Failure(FindingCodes.ArgumentsInvalid, "arguments", "Die Tool-Argumente sind kein gültiges JSON."));
            }

            using (parsed)
            {
                return HandleObjectAsync(parsed.RootElement.Clone(), cancellationToken);
            }
        }

        return HandleObjectAsync(arguments, cancellationToken);
    }

    /// <summary>Serializes a result in the contract shape <c>{ "findings": [...], "computed": { ... } }</c>.</summary>
    public static string SerializeResult(PlausibilityResult result) => JsonSerializer.Serialize(result, ResultJsonOptions);

    private Task<PlausibilityResult> HandleObjectAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult(Failure(FindingCodes.ArgumentsInvalid, "arguments", "Die Tool-Argumente müssen ein JSON-Objekt mit documentType und document sein."));
        }

        var typeName = arguments.TryGetProperty("documentType", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString()
            : null;
        if (!DocumentTypeNames.TryParse(typeName, out var documentType))
        {
            return Task.FromResult(Failure(FindingCodes.DocumentTypeInvalid, "documentType",
                $"Der Dokumenttyp „{typeName ?? "(fehlt)"}“ ist unbekannt. Erlaubt sind: {string.Join(", ", DocumentTypeNames.All)}."));
        }

        if (!arguments.TryGetProperty("document", out var document) || document.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult(Failure(FindingCodes.ArgumentsInvalid, "document", "Das Dokument fehlt oder ist kein JSON-Objekt."));
        }

        return _checker.CheckAsync(documentType.Value, document, cancellationToken);
    }

    private static PlausibilityResult Failure(string code, string path, string message) =>
        new([new PlausibilityFinding(code, FindingSeverity.Error, path, message)], new Dictionary<string, decimal>());

    private static JsonElement BuildParametersSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["documentType"] = new
                {
                    type = "string",
                    @enum = DocumentTypeNames.All,
                    description = "Typ des zu prüfenden Dokuments.",
                },
                ["document"] = new
                {
                    type = "object",
                    description = "Vollständiges Dokument-JSON gemäß schemas/<documentType>.schema.json.",
                },
            },
            required = new[] { "documentType", "document" },
            additionalProperties = false,
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(schema));
        return document.RootElement.Clone();
    }
}
