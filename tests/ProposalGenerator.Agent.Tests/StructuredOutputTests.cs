using System.Text.Json.Nodes;
using ProposalGenerator.Agent.Schemas;
using ProposalGenerator.Agent.StructuredOutput;

namespace ProposalGenerator.Agent.Tests;

public class StructuredOutputTests
{
    private static DocumentSchemaSet Schemas() => TestConfiguration.LoadRealDefinition().ResponseFormat.Schemas;

    private static StructuredAgentConversation Conversation(ScriptedResponder responder, int? maxRepairAttempts = null) =>
        new(responder, Schemas(), maxRepairAttempts is null ? null : new StructuredOutputOptions { MaxRepairAttempts = maxRepairAttempts.Value });

    [Fact]
    public void Default_allows_at_most_two_corrections()
    {
        Assert.Equal(2, new StructuredOutputOptions().MaxRepairAttempts);
    }

    [Fact]
    public async Task Valid_reply_is_accepted_on_the_first_attempt()
    {
        var responder = new ScriptedResponder(Replies.Checked("awaiting_confirmation", TestPaths.ValidSow()));

        var result = await Conversation(responder).SendAsync("SOW für Fabrikam");

        Assert.Equal(1, result.Attempts);
        Assert.Equal(AgentResponseStatus.AwaitingConfirmation, result.Response.Status);
        Assert.Equal("sow", result.Response.DocumentType);
        Assert.Equal("Fabrikam AG", result.Response.Document!["customer"]!["name"]!.GetValue<string>());
        Assert.Single(responder.Messages);
    }

    [Fact]
    public async Task Needs_input_reply_with_a_partial_document_is_accepted()
    {
        var partial = new JsonObject { ["customer"] = new JsonObject { ["name"] = "Fabrikam AG" } };
        var responder = new ScriptedResponder(new AgentReply(Replies.Envelope(
            "needs_input", "sow", partial, questions: ["Welcher Festpreis gilt?"], missingFields: ["/pricing/total"])));

        var result = await Conversation(responder).SendAsync("SOW für Fabrikam");

        Assert.Equal(AgentResponseStatus.NeedsInput, result.Response.Status);
        Assert.Equal(["/pricing/total"], result.Response.MissingFields);
        Assert.Equal(["Welcher Festpreis gilt?"], result.Response.Questions);
    }

    [Fact]
    public async Task Invalid_reply_is_repaired_with_the_failing_path_in_the_repair_message()
    {
        var responder = new ScriptedResponder(Replies.Invalid(), Replies.Checked("awaiting_confirmation", TestPaths.ValidSow()));

        var result = await Conversation(responder).SendAsync("SOW für Fabrikam");

        Assert.Equal(2, result.Attempts);
        Assert.Equal(1, result.RepairAttempts);
        Assert.Equal(2, responder.Messages.Count);
        Assert.Contains("failed validation", responder.Messages[1]);
        Assert.Contains("- /document/pricing:", responder.Messages[1]);
        Assert.Contains("currency", responder.Messages[1]);
    }

    [Fact]
    public async Task Reply_that_stays_invalid_is_rejected_after_two_corrections_with_field_paths()
    {
        var responder = new ScriptedResponder(Replies.Invalid(), Replies.Invalid(), Replies.Invalid(), Replies.Checked("complete", TestPaths.ValidSow()));

        var ex = await Assert.ThrowsAsync<StructuredOutputException>(() => Conversation(responder).SendAsync("SOW für Fabrikam"));

        Assert.Equal(3, ex.Attempts);
        Assert.Equal(3, responder.Messages.Count);
        Assert.Contains("/document/pricing", ex.FieldPaths);
        Assert.Contains(ex.Errors, e => e.Keyword == "required" && e.Message.Contains("currency"));
        Assert.Contains("/document/pricing", ex.Message);
        Assert.Equal(Replies.Invalid().Text, ex.LastOutput);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(4, 5)]
    public async Task Maximum_repair_attempts_is_configurable(int maxRepairAttempts, int expectedReplies)
    {
        var replies = Enumerable.Range(0, 6).Select(_ => Replies.Invalid()).ToArray();
        var responder = new ScriptedResponder(replies);

        var ex = await Assert.ThrowsAsync<StructuredOutputException>(
            () => Conversation(responder, maxRepairAttempts).SendAsync("SOW für Fabrikam"));

        Assert.Equal(expectedReplies, ex.Attempts);
        Assert.Equal(expectedReplies, responder.Messages.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Out_of_range_repair_attempts_are_rejected(int maxRepairAttempts)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Conversation(new ScriptedResponder(), maxRepairAttempts));
    }

    [Fact]
    public void Computed_totals_overwrite_the_model_values()
    {
        var sow = TestPaths.ValidSow();
        sow["pricing"]!["total"] = 47999;
        var computed = new JsonObject { ["$.pricing.total"] = 48000, ["/pricing/payment_schedule/1/amount"] = 24000 };

        var result = new AgentReplyValidator(Schemas()).Validate(
            Replies.Envelope("awaiting_confirmation", "sow", sow), [Replies.Plausibility(computed)]);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal(48000m, result.Response!.Document!["pricing"]!["total"]!.GetValue<decimal>());
        var applied = result.ComputedValues.Single(v => v.Path == "/document/pricing/total");
        Assert.Equal(47999, applied.ModelValue!.GetValue<int>());
        Assert.Equal(48000m, applied.ToolValue);
        Assert.Contains(result.ComputedValues, v => v.Path == "/document/pricing/payment_schedule/1/amount");
    }

    [Fact]
    public void Computed_value_for_a_missing_parent_is_an_error()
    {
        var computed = new JsonObject { ["$.nothing.total"] = 1 };

        var result = new AgentReplyValidator(Schemas()).Validate(
            Replies.Envelope("awaiting_confirmation", "sow", TestPaths.ValidSow()), [Replies.Plausibility(computed)]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "/document/nothing/total");
    }

    [Theory]
    [InlineData("awaiting_confirmation")]
    [InlineData("complete")]
    public void Final_reply_without_a_plausibility_check_is_rejected(string status)
    {
        var result = new AgentReplyValidator(Schemas()).Validate(Replies.Envelope(status, "sow", TestPaths.ValidSow()), []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Keyword == "check_plausibility" && e.Message.Contains("Call check_plausibility"));
    }

    [Fact]
    public void Complete_with_an_open_error_finding_is_rejected()
    {
        var call = Replies.Plausibility(null, ("PAYMENT_SUM_MISMATCH", "error", "$.pricing.payment_schedule"));

        var result = new AgentReplyValidator(Schemas()).Validate(Replies.Envelope("complete", "sow", TestPaths.ValidSow()), [call]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "/document/pricing/payment_schedule" && e.Message.Contains("PAYMENT_SUM_MISMATCH"));
    }

    [Fact]
    public void Awaiting_confirmation_with_findings_is_accepted_and_reports_them()
    {
        var call = Replies.Plausibility(null, ("PAYMENT_SUM_MISMATCH", "error", "$.pricing.payment_schedule"), ("LONG_DURATION", "warning", "$.sow"));

        var result = new AgentReplyValidator(Schemas()).Validate(Replies.Envelope("awaiting_confirmation", "sow", TestPaths.ValidSow()), [call]);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal(["PAYMENT_SUM_MISMATCH", "LONG_DURATION"], result.Findings.Select(f => f.Code));
    }

    [Theory]
    [InlineData("not json", "/", "not valid JSON")]
    [InlineData("[1, 2]", "/", "single JSON object")]
    public void Non_object_reply_is_rejected(string text, string expectedPath, string expectedMessage)
    {
        var result = new AgentReplyValidator(Schemas()).Validate(text, []);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(expectedPath, error.ToString()[..1]);
        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void Reply_with_a_duplicate_property_is_rejected_not_thrown()
    {
        var text = Replies.Envelope("needs_input", "sow", null, questions: ["Welcher Kunde?"]).Replace("\"status\":\"needs_input\"", "\"status\":\"needs_input\",\"status\":\"complete\"");
        Assert.Contains("\"status\":\"complete\"", text);

        var result = new AgentReplyValidator(Schemas()).Validate(text, []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Keyword == "json" && e.Message.Contains("status"));
    }

    [Fact]
    public void Nested_duplicate_property_in_the_document_is_rejected_not_thrown()
    {
        var text = Replies.Envelope("awaiting_confirmation", "sow", TestPaths.ValidSow()).Replace("\"total\":48000", "\"total\":48000,\"total\":1");
        Assert.Contains("\"total\":1", text);

        var result = new AgentReplyValidator(Schemas()).Validate(text, [Replies.Plausibility()]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Keyword == "json" && e.Message.Contains("total"));
    }

    [Fact]
    public void Duplicate_property_in_the_tool_output_is_rejected_not_thrown()
    {
        var call = Replies.Plausibility() with { Output = """{"findings":[],"computed":{"$.pricing.total":48000,"$.pricing.total":1}}""" };

        var result = new AgentReplyValidator(Schemas()).Validate(Replies.Envelope("awaiting_confirmation", "sow", TestPaths.ValidSow()), [call]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Keyword == "check_plausibility" && e.Message.Contains("unreadable"));
    }

    [Fact]
    public void Needs_input_without_a_question_is_rejected()
    {
        var result = new AgentReplyValidator(Schemas()).Validate(Replies.Envelope("needs_input", "sow", null), []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "/questions");
    }

    [Fact]
    public void More_than_three_questions_are_rejected()
    {
        var result = new AgentReplyValidator(Schemas()).Validate(
            Replies.Envelope("needs_input", "sow", null, questions: ["a?", "b?", "c?", "d?"]), []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "/questions");
    }

    [Fact]
    public void Final_reply_with_missing_fields_is_rejected()
    {
        var result = new AgentReplyValidator(Schemas()).Validate(
            Replies.Envelope("awaiting_confirmation", "sow", TestPaths.ValidSow(), missingFields: ["/pricing/total"]), [Replies.Plausibility()]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "/missingFields");
    }

    [Fact]
    public void Unknown_document_type_is_rejected()
    {
        var result = new AgentReplyValidator(Schemas()).Validate(
            Replies.Envelope("needs_input", "nda", null, questions: ["Welcher Typ?"]), []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "/documentType" && e.Message.Contains("change-request"));
    }

    [Fact]
    public void Unknown_envelope_property_is_rejected()
    {
        var reply = JsonNode.Parse(Replies.Envelope("needs_input", null, null, questions: ["Welcher Typ?"]))!.AsObject();
        reply["confidence"] = 0.9;

        var result = new AgentReplyValidator(Schemas()).Validate(reply.ToJsonString(), []);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Document_that_violates_a_format_is_rejected()
    {
        var sow = TestPaths.ValidSow();
        sow["document"]!["date"] = "1. September 2026";

        var result = new AgentReplyValidator(Schemas()).Validate(Replies.Envelope("awaiting_confirmation", "sow", sow), [Replies.Plausibility()]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "/document/document/date");
    }

    [Fact]
    public void Document_of_a_different_type_is_validated_against_its_own_schema()
    {
        var result = new AgentReplyValidator(Schemas()).Validate(
            Replies.Envelope("awaiting_confirmation", "msa", TestPaths.ValidSow()), [Replies.Plausibility(new JsonObject())]);

        Assert.False(result.IsValid);
        Assert.All(result.Errors, e => Assert.StartsWith("/document", e.Path));
    }
}
