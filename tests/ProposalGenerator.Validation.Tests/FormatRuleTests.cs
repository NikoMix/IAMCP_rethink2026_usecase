using static ProposalGenerator.Validation.FindingCodes;

namespace ProposalGenerator.Validation.Tests;

public sealed class ValidExamplesTests
{
    [Theory]
    [InlineData(DocumentType.Sow)]
    [InlineData(DocumentType.ChangeRequest)]
    [InlineData(DocumentType.Rfp)]
    [InlineData(DocumentType.Rfi)]
    [InlineData(DocumentType.Msa)]
    public async Task Valid_example_with_rate_card_and_registry_has_no_findings(DocumentType type)
    {
        var result = await Fixture.CheckAsync(type, Fixture.Load(type.ToName()));

        FindingAssert.Clean(result);
    }

    [Fact]
    public async Task Without_rate_card_and_registry_the_unverifiable_checks_are_reported_as_info()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, Fixture.Load("sow"), new PlausibilityCheckerOptions());

        Assert.All(result.Findings, f => Assert.Equal(FindingSeverity.Info, f.Severity));
        Assert.Equal([PredecessorUnverified, RateCardUnverified], result.Findings.Select(f => f.Code).Order());
    }
}

public sealed class FormatRuleTests
{
    [Fact]
    public async Task Matching_document_type_passes() =>
        FindingAssert.None(await Fixture.CheckAsync(DocumentType.Sow, _ => { }), DocumentTypeMismatch);

    [Fact]
    public async Task Document_type_that_differs_from_the_argument_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("document")["type"] = "rfp");

        FindingAssert.Single(result, DocumentTypeMismatch, FindingSeverity.Error, "$.document.type");
    }

    [Fact]
    public async Task Calendar_dates_pass() =>
        FindingAssert.None(await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("project")["end_date"] = "2028-02-29"), DateInvalid);

    [Theory]
    [InlineData("2026-02-30")]
    [InlineData("30.09.2026")]
    [InlineData("2026-9-30")]
    public async Task Invalid_date_fails(string value)
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("project")["end_date"] = value);

        var finding = FindingAssert.Single(result, DateInvalid, FindingSeverity.Error, "$.project.end_date");
        Assert.Contains(value, finding.Message);
    }

    [Fact]
    public async Task Invalid_date_in_an_array_item_reports_the_item_path()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("sow").Item("milestones", 1)["date"] = "Q3 2026");

        FindingAssert.Single(result, DateInvalid, FindingSeverity.Error, "$.sow.milestones[1].date");
    }

    [Fact]
    public async Task Numbers_pass() =>
        FindingAssert.None(await Fixture.CheckAsync(DocumentType.Sow, _ => { }), NumberInvalid);

    [Fact]
    public async Task Number_given_as_text_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("pricing")["total"] = "184.000 EUR");

        FindingAssert.Single(result, NumberInvalid, FindingSeverity.Error, "$.pricing.total");
    }

    [Fact]
    public async Task Negative_net_change_of_a_change_request_is_allowed()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, d =>
        {
            var impact = d.Obj("cr").Obj("impact");
            impact["cost"] = -12000;
            impact["revised_value"] = 172000;
            var item = impact.Item("cost_items", 0);
            item["quantity"] = -12;
            item["amount"] = -12000;
        });

        FindingAssert.Clean(result);
    }

    [Theory]
    [InlineData("days", "$.pricing.rate_card[1].days")]
    [InlineData("daily_rate", "$.pricing.rate_card[1].daily_rate")]
    public async Task Negative_quantity_or_rate_fails(string field, string path)
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("pricing").Item("rate_card", 1)[field] = -5);

        FindingAssert.Single(result, NegativeValue, FindingSeverity.Error, path);
    }

    [Fact]
    public async Task Negative_amount_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Msa, d => d.Obj("legal").Obj("liability_cap")["amount"] = -1);

        FindingAssert.Single(result, NegativeValue, FindingSeverity.Error, "$.legal.liability_cap.amount");
    }

    [Fact]
    public async Task Zero_is_not_negative() =>
        FindingAssert.None(await Fixture.CheckAsync(DocumentType.Msa, d => d.Obj("legal").Obj("liability_cap")["amount"] = 0), NegativeValue);
}
