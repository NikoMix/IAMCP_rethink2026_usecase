using ProposalGenerator.Validation.RateCards;
using static ProposalGenerator.Validation.FindingCodes;

namespace ProposalGenerator.Validation.Tests;

public sealed class RateCardComplianceRuleTests
{
    private static PlausibilityCheckerOptions RateCardOnly(params RateCard[] rateCards) => new()
    {
        RateCardProvider = new StaticRateCardProvider(rateCards.Length == 0 ? [Fixture.RateCard] : rateCards),
    };

    [Fact]
    public async Task Configured_rate_card_verifies_the_rates() =>
        FindingAssert.None(await Fixture.CheckAsync(DocumentType.Sow, _ => { }, RateCardOnly()), RateCardUnverified);

    [Fact]
    public async Task Missing_rate_card_is_reported_as_unverified()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, _ => { }, new PlausibilityCheckerOptions());

        FindingAssert.Single(result, RateCardUnverified, FindingSeverity.Info, "$.pricing.rate_card[0].daily_rate");
    }

    [Fact]
    public async Task Missing_document_date_is_reported_as_unverified()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("document").Remove("date"), RateCardOnly());

        FindingAssert.Single(result, RateCardUnverified, FindingSeverity.Info, "$.document.date");
    }

    [Theory]
    [InlineData("2026-01-01")]
    [InlineData("2026-12-31")]
    public async Task Document_date_on_a_validity_boundary_passes(string date)
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("document")["date"] = date, RateCardOnly());

        FindingAssert.None(result, RateCardNotValid);
        FindingAssert.None(result, DailyRateMismatch);
    }

    [Theory]
    [InlineData("2025-12-31")]
    [InlineData("2027-01-01")]
    public async Task Document_date_outside_the_validity_fails(string date)
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("document")["date"] = date, RateCardOnly());

        var finding = FindingAssert.Single(result, RateCardNotValid, FindingSeverity.Error, "$.document.date");
        Assert.Contains("RC-2026", finding.Message);
    }

    [Fact]
    public async Task Rate_card_valid_on_the_document_date_is_selected()
    {
        var older = Fixture.RateCard with
        {
            Id = "RC-2025",
            ValidFrom = new DateOnly(2025, 1, 1),
            ValidTo = new DateOnly(2025, 12, 31),
            Roles = Fixture.RateCard.Roles.Select(r => r with { DailyRate = r.DailyRate - 50 }).ToArray(),
        };

        var current = await Fixture.CheckAsync(DocumentType.Sow, _ => { }, RateCardOnly(older, Fixture.RateCard));
        var previous = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("document")["date"] = "2025-11-03", RateCardOnly(older, Fixture.RateCard));

        FindingAssert.None(current, DailyRateMismatch);
        Assert.Equal(2, previous.Findings.Count(f => f.Code == DailyRateMismatch && f.Message.Contains("RC-2025")));
    }

    [Fact]
    public async Task Document_in_the_rate_card_currency_passes() =>
        FindingAssert.None(await Fixture.CheckAsync(DocumentType.Sow, _ => { }, RateCardOnly()), RateCardCurrencyMismatch);

    [Fact]
    public async Task Document_in_another_currency_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("pricing")["currency"] = "CHF", RateCardOnly());

        FindingAssert.Single(result, RateCardCurrencyMismatch, FindingSeverity.Error, "$.pricing.currency");
        FindingAssert.None(result, DailyRateMismatch);
    }

    [Theory]
    [InlineData("Software Engineer")]
    [InlineData("  developer ")]
    [InlineData("developer")]
    public async Task Role_found_by_alias_or_id_ignoring_case_passes(string role)
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("pricing").Item("rate_card", 1)["role"] = role, RateCardOnly());

        FindingAssert.None(result, RoleNotInRateCard);
        FindingAssert.None(result, DailyRateMismatch);
    }

    [Fact]
    public async Task Role_missing_from_the_rate_card_is_a_warning_that_asks_for_the_rate()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d => d.Obj("pricing").Item("rate_card", 0)["role"] = "Quantum Consultant", RateCardOnly());

        var finding = FindingAssert.Single(result, RoleNotInRateCard, FindingSeverity.Warning, "$.pricing.rate_card[0].role");
        Assert.Contains("erfragen", finding.Message);
        Assert.DoesNotContain("$.pricing.rate_card[0].daily_rate", result.Computed.Keys);
    }

    [Fact]
    public async Task Change_request_cost_category_missing_from_the_rate_card_is_info()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, d => d.Obj("cr").Obj("impact").Item("cost_items", 0)["category"] = "Reisekosten", RateCardOnly());

        FindingAssert.Single(result, RoleNotInRateCard, FindingSeverity.Info, "$.cr.impact.cost_items[0].category");
    }

    [Fact]
    public async Task Daily_rate_from_the_rate_card_passes_and_is_computed()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, _ => { }, RateCardOnly());

        FindingAssert.None(result, DailyRateMismatch);
        Assert.Equal(1200m, result.Computed["$.pricing.rate_card[0].daily_rate"]);
    }

    [Fact]
    public async Task Daily_rate_that_differs_from_the_rate_card_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.Sow, d =>
        {
            var line = d.Obj("pricing").Item("rate_card", 1);
            line["daily_rate"] = 950;
            line["subtotal"] = 83600;
        }, RateCardOnly());

        var finding = FindingAssert.Single(result, DailyRateMismatch, FindingSeverity.Error, "$.pricing.rate_card[1].daily_rate");
        Assert.Contains("1.000,00 EUR", finding.Message);
    }

    [Fact]
    public async Task Change_request_rate_that_differs_from_the_rate_card_fails()
    {
        var result = await Fixture.CheckAsync(DocumentType.ChangeRequest, d =>
        {
            var impact = d.Obj("cr").Obj("impact");
            impact.Item("cost_items", 0)["rate"] = 1100;
            impact.Item("cost_items", 0)["amount"] = 13200;
            impact["cost"] = 13200;
            impact["revised_value"] = 197200;
        }, RateCardOnly());

        FindingAssert.Single(result, DailyRateMismatch, FindingSeverity.Error, "$.cr.impact.cost_items[0].rate");
    }
}

public sealed class RateCardLoaderTests
{
    [Fact]
    public void Knowledge_rate_card_loads()
    {
        var card = Fixture.RateCard;

        Assert.Equal("RC-2026", card.Id);
        Assert.Equal("EUR", card.Currency);
        Assert.Equal(new DateOnly(2026, 1, 1), card.ValidFrom);
        Assert.Equal(new DateOnly(2026, 12, 31), card.ValidTo);
        Assert.Equal(18, card.Roles.Count);
        Assert.Equal("rate-card.json", card.Source);
        Assert.Equal(1000m, card.FindRole("SOFTWARE  engineer")!.DailyRate);
    }

    [Fact]
    public void Validity_includes_both_bounds()
    {
        var card = Fixture.RateCard;

        Assert.True(card.IsValidOn(new DateOnly(2026, 1, 1)));
        Assert.True(card.IsValidOn(new DateOnly(2026, 12, 31)));
        Assert.False(card.IsValidOn(new DateOnly(2025, 12, 31)));
        Assert.False(card.IsValidOn(new DateOnly(2027, 1, 1)));
    }

    private const string Valid = """
        { "rate_card_id": "RC-T", "currency": "EUR", "valid_from": "2026-01-01", "valid_to": "2026-12-31",
          "roles": [ { "id": "dev", "role": "Developer", "aliases": ["Engineer"], "daily_rate": 1000 } ] }
        """;

    [Fact]
    public void Minimal_rate_card_parses() => Assert.Equal("dev", RateCardLoader.Parse(Valid, "t.json").FindRole("engineer")!.Id);

    [Theory]
    [InlineData("\"EUR\"", "\"Euro\"", "currency")]
    [InlineData("\"2026-12-31\"", "\"2025-12-31\"", "valid_to")]
    [InlineData("\"daily_rate\": 1000", "\"daily_rate\": -1", "daily_rate")]
    [InlineData("\"daily_rate\": 1000", "\"daily_rate\": \"1000\"", "daily_rate")]
    [InlineData("\"daily_rate\": 1000 } ]", "\"daily_rate\": 1000 }, { \"id\": \"eng\", \"role\": \"Engineer\", \"daily_rate\": 900 } ]", "more than one role")]
    [InlineData("\"rate_card_id\": \"RC-T\",", "", "rate_card_id")]
    [InlineData("{ \"rate_card_id\"", "{ , \"rate_card_id\"", "not valid JSON")]
    public void Invalid_rate_card_is_rejected(string find, string replace, string expected)
    {
        var json = Valid.Replace(find, replace, StringComparison.Ordinal);
        Assert.NotEqual(Valid, json);

        var exception = Assert.Throws<InvalidDataException>(() => RateCardLoader.Parse(json, "t.json"));
        Assert.Contains(expected, exception.Message);
    }
}
