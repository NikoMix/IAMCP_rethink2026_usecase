using static ProposalGenerator.Validation.FindingCodes;

namespace ProposalGenerator.Validation.Tests;

public sealed class DateRuleTests
{
    [Fact]
    public async Task Start_on_the_same_day_as_end_passes()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("project")["end_date"] = "2026-04-01");

        FindingAssert.None(result, ProjectPeriodInvalid);
    }

    [Fact]
    public async Task End_before_start_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("project")["end_date"] = "2026-03-31");

        var finding = FindingAssert.Single(result, ProjectPeriodInvalid, FindingSeverity.Error, "$.project.end_date");
        Assert.Contains("31.03.2026", finding.Message);
        FindingAssert.None(result, MilestoneOutsidePeriod);
    }

    [Fact]
    public async Task Deadlines_on_the_same_day_pass()
    {
        var result = await Fixture.CheckAsync(DocumentType.Rfp, d => d.Obj("rfp")["questions_deadline"] = "2026-02-27");

        FindingAssert.None(result, DeadlineOrderInvalid);
    }

    [Fact]
    public async Task Questions_deadline_after_submission_deadline_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Rfp, d => d.Obj("rfp")["questions_deadline"] = "2026-02-28");

        FindingAssert.Single(result, DeadlineOrderInvalid, FindingSeverity.Error, "$.rfp.submission_deadline");
    }

    [Fact]
    public async Task Rfi_response_deadline_before_issue_date_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Rfi, d =>
        {
            d.Obj("rfi")["questions_deadline"] = "2026-01-10";
            d.Obj("rfi")["response_deadline"] = "2026-01-11";
        });

        Assert.Equal(["$.rfi.questions_deadline", "$.rfi.response_deadline"],
            result.Findings.Where(f => f.Code == DeadlineOrderInvalid).Select(f => f.Path).Order());
    }

    [Fact]
    public async Task Change_request_decision_before_request_date_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, d => d.Obj("cr")["decision_due"] = "2026-06-09");

        FindingAssert.Single(result, DeadlineOrderInvalid, FindingSeverity.Error, "$.cr.decision_due");
    }

    [Theory]
    [InlineData("2026-04-01")]
    [InlineData("2026-09-30")]
    public async Task Milestone_on_a_period_boundary_passes(string date)
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("sow").Item("milestones", 1)["date"] = date);

        FindingAssert.None(result, MilestoneOutsidePeriod);
    }

    [Theory]
    [InlineData("2026-03-31", "vor dem Projektstart")]
    [InlineData("2026-10-01", "nach dem Projektende")]
    public async Task Milestone_outside_the_period_fails(string date, string expectedText)
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("sow").Item("milestones", 1)["date"] = date);

        var finding = FindingAssert.Single(result, MilestoneOutsidePeriod, FindingSeverity.Warning, "$.sow.milestones[1].date");
        Assert.Contains("„Go-live“", finding.Message);
        Assert.Contains(expectedText, finding.Message);
    }

    [Fact]
    public async Task Deliverable_within_the_period_passes() =>
        FindingAssert.None(await Fixture.CheckAsync(DocumentType.Sow, _ => { }), DeliverableOutsidePeriod);

    [Fact]
    public async Task Deliverable_due_after_the_period_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("sow").Item("deliverables", 0)["due_date"] = "2026-12-01");

        FindingAssert.Single(result, DeliverableOutsidePeriod, FindingSeverity.Warning, "$.sow.deliverables[0].due_date");
    }
}
