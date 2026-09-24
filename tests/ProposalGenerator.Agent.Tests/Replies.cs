using System.Text.Json.Nodes;
using ProposalGenerator.Agent.StructuredOutput;

namespace ProposalGenerator.Agent.Tests;

/// <summary>Builds agent replies and check_plausibility tool calls for tests.</summary>
internal static class Replies
{
    public static string Envelope(
        string status,
        string? documentType,
        JsonObject? document,
        string[]? questions = null,
        string[]? missingFields = null,
        string language = "de",
        string message = "Zusammenfassung")
    {
        var reply = new JsonObject
        {
            ["status"] = status,
            ["language"] = language,
            ["message"] = message,
            ["documentType"] = documentType,
            ["questions"] = new JsonArray((questions ?? []).Select(q => (JsonNode?)q).ToArray()),
            ["missingFields"] = new JsonArray((missingFields ?? []).Select(f => (JsonNode?)f).ToArray()),
            ["document"] = document,
        };
        return reply.ToJsonString();
    }

    public static ToolCallRecord Plausibility(JsonObject? computed = null, params (string Code, string Severity, string Path)[] findings)
    {
        var output = new JsonObject
        {
            ["findings"] = new JsonArray(findings.Select(f => (JsonNode?)new JsonObject
            {
                ["code"] = f.Code,
                ["severity"] = f.Severity,
                ["path"] = f.Path,
                ["message"] = $"{f.Code} message",
            }).ToArray()),
            ["computed"] = computed ?? new JsonObject { ["$.pricing.total"] = 48000 },
        };
        return new ToolCallRecord("check_plausibility", """{"documentType":"sow","document":{}}""", output.ToJsonString());
    }

    public static AgentReply Checked(string status, JsonObject document, ToolCallRecord? call = null) =>
        new(Envelope(status, "sow", document), [call ?? Plausibility()]);

    public static AgentReply Invalid() => new(Envelope("awaiting_confirmation", "sow", WithoutCurrency()), [Plausibility()]);

    public static JsonObject WithoutCurrency()
    {
        var sow = TestPaths.ValidSow();
        sow["pricing"]!.AsObject().Remove("currency");
        return sow;
    }
}
