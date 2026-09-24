using System.Text.Json.Nodes;
using static ProposalGenerator.Validation.FindingCodes;

namespace ProposalGenerator.Validation.Tests;

public sealed class CurrencyRuleTests
{
    [Fact]
    public async Task Single_iso_currency_passes()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, _ => { });

        FindingAssert.None(result, CurrencyInvalid);
        FindingAssert.None(result, CurrencyMixed);
    }

    [Theory]
    [InlineData("Euro")]
    [InlineData("eur")]
    [InlineData("€")]
    public async Task Non_iso_currency_fails(string currency)
    {
        var result = await Fixture.CheckAsync(DocumentType.Msa, d => d.Obj("legal").Obj("liability_cap")["currency"] = currency);

        FindingAssert.Single(result, CurrencyInvalid, FindingSeverity.Error, "$.legal.liability_cap.currency");
    }

    [Fact]
    public async Task Second_currency_in_the_document_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("pricing").Item("payment_schedule", 1)["currency"] = "USD");

        var finding = FindingAssert.Single(result, CurrencyMixed, FindingSeverity.Error, "$.pricing.payment_schedule[1].currency");
        Assert.Contains("„EUR“", finding.Message);
    }
}

public sealed class RateCardArithmeticRuleTests
{
    [Fact]
    public async Task Subtotal_equal_to_rate_times_days_passes_and_is_computed()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, _ => { });

        FindingAssert.None(result, RateLineSubtotalMismatch);
        Assert.Equal(96000m, result.Computed["$.pricing.rate_card[0].subtotal"]);
        Assert.Equal(88000m, result.Computed["$.pricing.rate_card[1].subtotal"]);
    }

    [Fact]
    public async Task Subtotal_is_compared_in_cents()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d =>
        {
            var line = d.Obj("pricing").Item("rate_card", 1);
            // 1000 × 0.333325 = 333.325: commercial rounding gives 333.33, banker's rounding would give 333.32.
            line["days"] = 0.333325;
            line["subtotal"] = 333.33;
        });

        FindingAssert.None(result, RateLineSubtotalMismatch);
        Assert.Equal(333.33m, result.Computed["$.pricing.rate_card[1].subtotal"]);
    }

    [Fact]
    public async Task Subtotal_rounded_the_wrong_way_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d =>
        {
            var line = d.Obj("pricing").Item("rate_card", 1);
            line["days"] = 0.333325;
            line["subtotal"] = 333.32;
        });

        FindingAssert.Single(result, RateLineSubtotalMismatch, FindingSeverity.Error, "$.pricing.rate_card[1].subtotal");
    }

    [Fact]
    public async Task Wrong_subtotal_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("pricing").Item("rate_card", 0)["subtotal"] = 96000.01);

        var finding = FindingAssert.Single(result, RateLineSubtotalMismatch, FindingSeverity.Error, "$.pricing.rate_card[0].subtotal");
        Assert.Contains("96.000,00", finding.Message);
    }

    [Fact]
    public async Task Time_and_materials_total_equal_to_the_sum_passes()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("pricing")["model"] = "time_and_materials");

        FindingAssert.None(result, PricingTotalMismatch);
        Assert.Equal(184000m, result.Computed["$.pricing.total"]);
    }

    [Fact]
    public async Task Time_and_materials_total_that_differs_from_the_sum_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d =>
        {
            d.Obj("pricing")["model"] = "time_and_materials";
            d.Obj("pricing")["total"] = 180000;
        });

        FindingAssert.Single(result, PricingTotalMismatch, FindingSeverity.Error, "$.pricing.total");
    }

    [Fact]
    public async Task Fixed_price_equal_to_the_estimate_passes() =>
        FindingAssert.None(await Fixture.CheckAsync(DocumentType.Sow, _ => { }), FixedPriceDiffersFromEstimate);

    [Fact]
    public async Task Fixed_price_that_differs_from_the_estimate_is_info()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("pricing")["total"] = 180000);

        FindingAssert.Single(result, FixedPriceDiffersFromEstimate, FindingSeverity.Info, "$.pricing.total");
        FindingAssert.None(result, PricingTotalMismatch);
    }

    [Fact]
    public async Task Capped_total_equal_to_the_cap_passes()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d =>
        {
            d.Obj("pricing")["model"] = "capped_tm";
            d.Obj("pricing")["cap"] = 184000;
        });

        FindingAssert.None(result, PricingCapExceeded);
        FindingAssert.None(result, PricingTotalMismatch);
    }

    [Fact]
    public async Task Capped_total_above_the_cap_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d =>
        {
            d.Obj("pricing")["model"] = "capped_tm";
            d.Obj("pricing")["cap"] = 183999.99;
        });

        FindingAssert.Single(result, PricingCapExceeded, FindingSeverity.Error, "$.pricing.total");
    }
}

public sealed class PaymentScheduleRuleTests
{
    private static void SetPercentages(JsonObject document, params double[] percentages)
    {
        var schedule = document.Obj("pricing")["payment_schedule"]!.AsArray();
        for (var i = 0; i < percentages.Length; i++)
        {
            schedule[i]!["percentage"] = percentages[i];
        }
    }

    [Fact]
    public async Task Schedule_that_adds_up_to_the_total_passes() =>
        FindingAssert.None(await Fixture.CheckAsync(DocumentType.Sow, _ => { }), PaymentScheduleSumMismatch);

    [Fact]
    public async Task Schedule_that_does_not_add_up_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("pricing").Item("payment_schedule", 1)["amount"] = 128799.99);

        var finding = FindingAssert.Single(result, PaymentScheduleSumMismatch, FindingSeverity.Error, "$.pricing.payment_schedule");
        Assert.Contains("0,01", finding.Message);
    }

    [Fact]
    public async Task Percentages_that_add_up_to_100_pass()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => SetPercentages(d, 30, 70));

        FindingAssert.Clean(result);
        Assert.Equal(55200m, result.Computed["$.pricing.payment_schedule[0].amount"]);
    }

    [Fact]
    public async Task Percentages_that_do_not_add_up_to_100_fail()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => SetPercentages(d, 30, 60));

        var finding = FindingAssert.Single(result, PaymentPercentageSumInvalid, FindingSeverity.Error, "$.pricing.payment_schedule");
        Assert.Contains("90 %", finding.Message);
    }

    [Fact]
    public async Task Percentages_on_only_some_entries_fail()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => SetPercentages(d, 30));

        FindingAssert.Single(result, PaymentPercentageSumInvalid, FindingSeverity.Error, "$.pricing.payment_schedule");
    }

    [Fact]
    public async Task Amount_equal_to_its_percentage_passes() =>
        FindingAssert.None(await Fixture.CheckAsync(DocumentType.Sow, d => SetPercentages(d, 30, 70)), PaymentAmountPercentageMismatch);

    [Fact]
    public async Task Amount_that_differs_from_its_percentage_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d =>
        {
            SetPercentages(d, 30, 70);
            d.Obj("pricing").Item("payment_schedule", 0)["amount"] = 55000;
            d.Obj("pricing").Item("payment_schedule", 1)["amount"] = 129000;
        });

        var findings = result.Findings.Where(f => f.Code == PaymentAmountPercentageMismatch).ToArray();
        Assert.Equal(["$.pricing.payment_schedule[0].amount", "$.pricing.payment_schedule[1].amount"], findings.Select(f => f.Path));
        Assert.All(findings, f => Assert.Equal(FindingSeverity.Error, f.Severity));
        FindingAssert.None(result, PaymentScheduleSumMismatch);
    }
}

public sealed class EvaluationWeightsRuleTests
{
    [Fact]
    public async Task Weights_that_add_up_to_100_pass() =>
        FindingAssert.None(await Fixture.CheckAsync(DocumentType.Rfp, _ => { }), EvaluationWeightsSumInvalid);

    [Fact]
    public async Task Weights_that_do_not_add_up_to_100_fail()
    {
        var result = await Fixture.CheckAsync(DocumentType.Rfp, d => d.Obj("rfp").Item("evaluation_criteria", 2)["weight"] = 25);

        var finding = FindingAssert.Single(result, EvaluationWeightsSumInvalid, FindingSeverity.Error, "$.rfp.evaluation_criteria");
        Assert.Contains("105 %", finding.Message);
    }
}

public sealed class ChangeRequestArithmeticRuleTests
{
    [Fact]
    public async Task Consistent_change_request_passes_and_values_are_computed()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, _ => { });

        FindingAssert.Clean(result);
        Assert.Equal(12000m, result.Computed["$.cr.impact.cost_items[0].amount"]);
        Assert.Equal(12000m, result.Computed["$.cr.impact.cost"]);
        Assert.Equal(196000m, result.Computed["$.cr.impact.revised_value"]);
    }

    [Fact]
    public async Task Cost_item_amount_that_differs_from_quantity_times_rate_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, d =>
        {
            var impact = d.Obj("cr").Obj("impact");
            impact.Item("cost_items", 0)["amount"] = 11000;
            impact["cost"] = 11000;
            impact["revised_value"] = 195000;
        });

        FindingAssert.Single(result, CrCostItemAmountMismatch, FindingSeverity.Error, "$.cr.impact.cost_items[0].amount");
        FindingAssert.None(result, CrCostSumMismatch);
        FindingAssert.None(result, CrRevisedValueMismatch);
    }

    [Fact]
    public async Task Net_cost_that_differs_from_the_cost_items_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, d =>
        {
            var impact = d.Obj("cr").Obj("impact");
            impact["cost"] = 13000;
            impact["revised_value"] = 197000;
        });

        FindingAssert.Single(result, CrCostSumMismatch, FindingSeverity.Error, "$.cr.impact.cost");
        FindingAssert.None(result, CrRevisedValueMismatch);
    }

    [Fact]
    public async Task Revised_value_that_differs_from_original_plus_cost_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, d => d.Obj("cr").Obj("impact")["revised_value"] = 195000);

        var finding = FindingAssert.Single(result, CrRevisedValueMismatch, FindingSeverity.Error, "$.cr.impact.revised_value");
        Assert.Contains("196.000,00", finding.Message);
    }
}
