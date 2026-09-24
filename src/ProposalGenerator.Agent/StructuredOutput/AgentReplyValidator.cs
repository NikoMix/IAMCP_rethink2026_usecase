using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProposalGenerator.Agent.Schemas;

namespace ProposalGenerator.Agent.StructuredOutput;

/// <summary>Validates one agent reply against the envelope, the rules around check_plausibility, and the document schema.</summary>
public sealed class AgentReplyValidator
{
    private readonly DocumentSchemaSet _schemas;
    private readonly string? _plausibilityTool;

    public AgentReplyValidator(DocumentSchemaSet schemas, string? plausibilityToolName = StructuredOutputOptions.DefaultPlausibilityToolName)
    {
        _schemas = schemas ?? throw new ArgumentNullException(nameof(schemas));
        _plausibilityTool = plausibilityToolName;
    }

    /// <param name="replyText">Raw text of the agent reply.</param>
    /// <param name="toolCalls">Function tool calls made in the current turn, oldest first.</param>
    public ReplyValidationResult Validate(string replyText, IReadOnlyList<ToolCallRecord> toolCalls)
    {
        ArgumentNullException.ThrowIfNull(toolCalls);

        JsonObject reply;
        try
        {
            if (StrictJson.Parse(replyText ?? string.Empty) is not JsonObject parsed)
            {
                return ReplyValidationResult.Invalid(new SchemaValidationError(string.Empty, "json", "The reply must be a single JSON object."));
            }

            reply = parsed;
        }
        catch (JsonException ex)
        {
            return ReplyValidationResult.Invalid(new SchemaValidationError(string.Empty, "json", $"The reply is not valid JSON: {ex.Message}"));
        }

        var envelopeErrors = Validate(reply, _schemas.ValidateEnvelope);
        if (envelopeErrors.Count > 0)
        {
            return ReplyValidationResult.Invalid(envelopeErrors);
        }

        var status = ParseStatus(reply["status"]!.GetValue<string>());
        var documentType = reply["documentType"]?.GetValue<string>();
        var document = reply["document"] as JsonObject;
        var errors = new List<SchemaValidationError>();
        var applied = new List<AppliedComputedValue>();
        IReadOnlyList<PlausibilityFinding> findings = [];

        if (documentType is not null && !_schemas.Supports(documentType))
        {
            errors.Add(new SchemaValidationError("/documentType", "enum",
                $"Unknown document type '{documentType}'. Use one of: {string.Join(", ", _schemas.DocumentTypes)}."));
        }

        if (status is AgentResponseStatus.AwaitingConfirmation or AgentResponseStatus.Complete && errors.Count == 0 && document is not null)
        {
            if (_plausibilityTool is not null)
            {
                var call = toolCalls.LastOrDefault(c => c.Name == _plausibilityTool);
                if (call is null)
                {
                    errors.Add(new SchemaValidationError("/document", _plausibilityTool,
                        $"Call {_plausibilityTool} with the current document before setting the status to {reply["status"]}."));
                }
                else
                {
                    findings = ApplyPlausibility(call, status, document, errors, applied);
                }
            }

            errors.AddRange(Validate(document, element => _schemas.ValidateDocument(documentType!, element, "/document")));
        }

        if (errors.Count > 0)
        {
            return ReplyValidationResult.Invalid(errors);
        }

        var response = new AgentResponse
        {
            Status = status,
            Language = reply["language"]!.GetValue<string>(),
            Message = reply["message"]!.GetValue<string>(),
            DocumentType = documentType,
            Questions = reply["questions"]!.AsArray().Select(q => q!.GetValue<string>()).ToArray(),
            MissingFields = reply["missingFields"]!.AsArray().Select(f => f!.GetValue<string>()).ToArray(),
            Document = document,
        };

        return ReplyValidationResult.Valid(response, applied, findings);
    }

    private IReadOnlyList<PlausibilityFinding> ApplyPlausibility(
        ToolCallRecord call,
        AgentResponseStatus status,
        JsonObject document,
        List<SchemaValidationError> errors,
        List<AppliedComputedValue> applied)
    {
        JsonObject output;
        try
        {
            output = StrictJson.Parse(call.Output) as JsonObject
                ?? throw new JsonException("The output is not a JSON object.");
        }
        catch (JsonException ex)
        {
            errors.Add(new SchemaValidationError("/document", _plausibilityTool!, $"{_plausibilityTool} returned unreadable output: {ex.Message}"));
            return [];
        }

        var findings = new List<PlausibilityFinding>();
        foreach (var node in output["findings"] as JsonArray ?? [])
        {
            if (node is JsonObject finding)
            {
                findings.Add(new PlausibilityFinding(
                    Text(finding["code"]), Text(finding["severity"]), Text(finding["path"]), Text(finding["message"])));
            }
        }

        if (status == AgentResponseStatus.Complete)
        {
            foreach (var finding in findings.Where(f => f.Severity == "error"))
            {
                var path = DocumentPath.TryParse(finding.Path, out var segments) ? "/document" + DocumentPath.ToPointer(segments) : "/document";
                errors.Add(new SchemaValidationError(path, _plausibilityTool!,
                    $"{_plausibilityTool} reported an open error {finding.Code}: {finding.Message}. Resolve it before completing."));
            }
        }

        foreach (var (rawPath, value) in output["computed"] as JsonObject ?? [])
        {
            if (value is not JsonValue number || !number.TryGetValue(out decimal toolValue))
            {
                errors.Add(new SchemaValidationError("/document", _plausibilityTool!, $"{_plausibilityTool} computed a non-numeric value for '{rawPath}'."));
                continue;
            }

            if (!DocumentPath.TryParse(rawPath, out var segments))
            {
                errors.Add(new SchemaValidationError("/document", _plausibilityTool!, $"{_plausibilityTool} computed a value for the unsupported path '{rawPath}'."));
                continue;
            }

            var pointer = "/document" + DocumentPath.ToPointer(segments);
            if (!TrySet(document, segments, toolValue, out var previous))
            {
                errors.Add(new SchemaValidationError(pointer, _plausibilityTool!,
                    $"The value computed by {_plausibilityTool} cannot be applied because the field's parent does not exist. Call {_plausibilityTool} again with the current document."));
                continue;
            }

            applied.Add(new AppliedComputedValue(pointer, previous, toolValue));
        }

        return findings;
    }

    private static bool TrySet(JsonObject document, IReadOnlyList<string> segments, decimal value, out JsonNode? previous)
    {
        previous = null;
        JsonNode? parent = document;
        foreach (var segment in segments.Take(segments.Count - 1))
        {
            parent = parent switch
            {
                JsonObject obj => obj[segment],
                JsonArray array when int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i < array.Count => array[i],
                _ => null,
            };
        }

        var last = segments[^1];
        switch (parent)
        {
            case JsonObject obj:
                previous = obj[last]?.DeepClone();
                obj[last] = JsonValue.Create(value);
                return true;
            case JsonArray array when int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < array.Count:
                previous = array[index]?.DeepClone();
                array[index] = JsonValue.Create(value);
                return true;
            default:
                return false;
        }
    }

    private static IReadOnlyList<SchemaValidationError> Validate(JsonNode node, Func<JsonElement, IReadOnlyList<SchemaValidationError>> validate)
    {
        using var json = JsonDocument.Parse(node.ToJsonString());
        return validate(json.RootElement);
    }

    private static AgentResponseStatus ParseStatus(string status) => status switch
    {
        "needs_input" => AgentResponseStatus.NeedsInput,
        "awaiting_confirmation" => AgentResponseStatus.AwaitingConfirmation,
        "complete" => AgentResponseStatus.Complete,
        _ => throw new InvalidOperationException($"Status '{status}' passed the envelope schema but is unknown."),
    };

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out string? text) ? text : node?.ToJsonString() ?? string.Empty;
}

public sealed class ReplyValidationResult
{
    private ReplyValidationResult(
        AgentResponse? response,
        IReadOnlyList<SchemaValidationError> errors,
        IReadOnlyList<AppliedComputedValue> computed,
        IReadOnlyList<PlausibilityFinding> findings)
    {
        Response = response;
        Errors = errors;
        ComputedValues = computed;
        Findings = findings;
    }

    public bool IsValid => Response is not null;
    public AgentResponse? Response { get; }
    public IReadOnlyList<SchemaValidationError> Errors { get; }
    public IReadOnlyList<AppliedComputedValue> ComputedValues { get; }
    public IReadOnlyList<PlausibilityFinding> Findings { get; }

    internal static ReplyValidationResult Valid(AgentResponse response, IReadOnlyList<AppliedComputedValue> computed, IReadOnlyList<PlausibilityFinding> findings) =>
        new(response, [], computed, findings);

    internal static ReplyValidationResult Invalid(params IReadOnlyList<SchemaValidationError> errors) =>
        new(null, errors, [], []);
}
