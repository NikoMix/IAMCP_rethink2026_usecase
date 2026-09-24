using System.Text.Json;
using System.Text.RegularExpressions;
using ProposalGenerator.Validation.Internal;

namespace ProposalGenerator.Validation.Rules;

/// <summary>
/// Every currency in the document must be an ISO 4217 code, and a document uses exactly one currency.
/// The reference currency is <c>pricing.currency</c> when it is valid, otherwise the first currency found.
/// </summary>
internal sealed partial class CurrencyRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } = [FindingCodes.CurrencyInvalid, FindingCodes.CurrencyMixed];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        var valid = new List<(string Path, string Code)>();
        foreach (var (located, name) in JsonNavigator.Properties(context.Document, JsonNavigator.RootPath))
        {
            if (!string.Equals(name, context.Paths.CurrencyPropertyName, StringComparison.Ordinal)
                || located.Value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (located.Value.ValueKind == JsonValueKind.String && CurrencyCode().IsMatch(located.Value.GetString()!))
            {
                valid.Add((located.Path, located.Value.GetString()!));
            }
            else
            {
                context.Report(FindingCodes.CurrencyInvalid, FindingSeverity.Error, located.Path,
                    $"Die Währung „{FieldSets.Raw(located.Value)}“ ist kein gültiger ISO-4217-Code (zum Beispiel EUR).");
            }
        }

        if (valid.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        var pricingPath = RuleContext.PathOf(context.Paths.PricingCurrency);
        var reference = valid.FirstOrDefault(c => c.Path == pricingPath);
        if (reference.Code is null)
        {
            reference = valid[0];
        }

        foreach (var (path, code) in valid.Where(c => c.Code != reference.Code))
        {
            context.Report(FindingCodes.CurrencyMixed, FindingSeverity.Error, path,
                $"Das Dokument verwendet mehrere Währungen: „{code}“ weicht von der Dokumentwährung „{reference.Code}“ ab. Ein Dokument darf nur eine Währung enthalten.");
        }

        return ValueTask.CompletedTask;
    }

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyCode();
}

/// <summary>
/// Rate-card lines: subtotal = daily rate × days; for time and materials the total equals the sum of the subtotals;
/// a fixed price that deviates from that estimate is reported as info; a capped total must not exceed the cap.
/// </summary>
internal sealed class RateCardArithmeticRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } =
    [
        FindingCodes.RateLineSubtotalMismatch, FindingCodes.PricingTotalMismatch,
        FindingCodes.FixedPriceDiffersFromEstimate, FindingCodes.PricingCapExceeded,
    ];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        var p = context.Paths;
        var lines = context.Items(p.RateCardLines);
        decimal? estimate = lines.Count == 0 ? null : 0m;
        foreach (var line in lines)
        {
            var subtotalField = JsonNavigator.Find(line, p.RateCardLineSubtotal);
            if (!Values.TryGetNumber(JsonNavigator.Find(line, p.RateCardLineDailyRate), out var rate)
                || !Values.TryGetNumber(JsonNavigator.Find(line, p.RateCardLineDays), out var days))
            {
                estimate = null;
                continue;
            }

            var expected = Values.Money(rate * days);
            var subtotalPath = subtotalField?.Path ?? $"{line.Path}.{p.RateCardLineSubtotal}";
            context.SetComputed(subtotalPath, expected);
            estimate += expected;

            if (Values.TryGetNumber(subtotalField, out var subtotal) && Values.Money(subtotal) != expected)
            {
                context.Report(FindingCodes.RateLineSubtotalMismatch, FindingSeverity.Error, subtotalPath,
                    $"Die Zwischensumme {Values.FormatAmount(subtotal)} entspricht nicht Tagessatz × Tage ({Values.FormatAmount(rate)} × {Values.FormatNumber(days)} = {Values.FormatAmount(expected)}).");
            }
        }

        var model = Values.TryGetText(context.Find(p.PricingModel), out var m) ? m : null;
        var totalField = context.Find(p.PricingTotal);
        var hasTotal = Values.TryGetNumber(totalField, out var total);
        var totalPath = totalField?.Path ?? RuleContext.PathOf(p.PricingTotal);
        var isTimeAndMaterials = model == p.TimeAndMaterialsModel || model == p.CappedTimeAndMaterialsModel;

        if (estimate is { } sum && isTimeAndMaterials)
        {
            context.SetComputed(totalPath, sum);
            if (hasTotal && Values.Money(total) != sum)
            {
                context.Report(FindingCodes.PricingTotalMismatch, FindingSeverity.Error, totalPath,
                    $"Der geschätzte Gesamtpreis {Values.FormatAmount(total)} entspricht nicht der Summe der Zwischensummen der Rate Card ({Values.FormatAmount(sum)}).");
            }
        }
        else if (estimate is { } fixedEstimate && model == p.FixedPriceModel && hasTotal && Values.Money(total) != fixedEstimate)
        {
            context.Report(FindingCodes.FixedPriceDiffersFromEstimate, FindingSeverity.Info, totalPath,
                $"Der Festpreis {Values.FormatAmount(total)} weicht von der Aufwandsschätzung laut Rate Card ({Values.FormatAmount(fixedEstimate)}) um {Values.FormatAmount(total - fixedEstimate)} ab. Bitte bestätigen, dass die Abweichung gewollt ist.");
        }

        if (model == p.CappedTimeAndMaterialsModel && hasTotal && Values.TryGetNumber(context.Find(p.PricingCap), out var cap) && total > cap)
        {
            context.Report(FindingCodes.PricingCapExceeded, FindingSeverity.Error, totalPath,
                $"Der geschätzte Gesamtpreis {Values.FormatAmount(total)} überschreitet die vereinbarte Obergrenze von {Values.FormatAmount(cap)}.");
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The payment schedule adds up to the total price. When entries carry percentages, they add up to 100 and each
/// amount equals its percentage of the total.
/// </summary>
internal sealed class PaymentScheduleRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } =
    [
        FindingCodes.PaymentScheduleSumMismatch, FindingCodes.PaymentPercentageSumInvalid, FindingCodes.PaymentAmountPercentageMismatch,
    ];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        var p = context.Paths;
        var entries = context.Items(p.PaymentSchedule);
        if (entries.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        var schedulePath = RuleContext.PathOf(p.PaymentSchedule);
        var hasTotal = Values.TryGetNumber(context.Find(p.PricingTotal), out var total);

        var amounts = entries.Select(e => Values.TryGetNumber(JsonNavigator.Find(e, p.PaymentAmount), out var a) ? a : (decimal?)null).ToArray();
        if (hasTotal && amounts.All(a => a is not null))
        {
            var sum = Values.Money(amounts.Sum(a => a!.Value));
            if (sum != Values.Money(total))
            {
                context.Report(FindingCodes.PaymentScheduleSumMismatch, FindingSeverity.Error, schedulePath,
                    $"Die Summe des Zahlungsplans ({Values.FormatAmount(sum)}) entspricht nicht dem Gesamtpreis ({Values.FormatAmount(total)}); Differenz {Values.FormatAmount(total - sum)}.");
            }
        }

        var percentages = entries
            .Select(e => (Entry: e, Field: JsonNavigator.Find(e, p.PaymentPercentage)))
            .Where(x => x.Field is not null)
            .ToArray();
        if (percentages.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        if (percentages.Any(x => !Values.TryGetNumber(x.Field, out _)))
        {
            return ValueTask.CompletedTask;
        }

        if (percentages.Length == entries.Count)
        {
            var percentSum = percentages.Sum(x => Values.TryGetNumber(x.Field, out var v) ? v : 0);
            if (Values.Money(percentSum) != 100m)
            {
                context.Report(FindingCodes.PaymentPercentageSumInvalid, FindingSeverity.Error, schedulePath,
                    $"Die Prozentsätze des Zahlungsplans ergeben zusammen {Values.FormatNumber(percentSum)} % statt 100 %.");
            }
        }
        else
        {
            context.Report(FindingCodes.PaymentPercentageSumInvalid, FindingSeverity.Error, schedulePath,
                "Nur ein Teil der Zahlungsplan-Einträge hat einen Prozentsatz. Entweder alle Einträge oder keiner erhalten einen Prozentsatz.");
        }

        if (!hasTotal)
        {
            return ValueTask.CompletedTask;
        }

        foreach (var (entry, field) in percentages)
        {
            if (!Values.TryGetNumber(field, out var percentage))
            {
                continue;
            }

            var expected = Values.Money(total * percentage / 100m);
            var amountField = JsonNavigator.Find(entry, p.PaymentAmount);
            var amountPath = amountField?.Path ?? $"{entry.Path}.{p.PaymentAmount}";
            context.SetComputed(amountPath, expected);
            if (Values.TryGetNumber(amountField, out var amount) && Values.Money(amount) != expected)
            {
                context.Report(FindingCodes.PaymentAmountPercentageMismatch, FindingSeverity.Error, amountPath,
                    $"Der Betrag {Values.FormatAmount(amount)} entspricht nicht {Values.FormatNumber(percentage)} % des Gesamtpreises ({Values.FormatAmount(expected)}).");
            }
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>RFP evaluation weights add up to 100 percent.</summary>
internal sealed class EvaluationWeightsRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } = [FindingCodes.EvaluationWeightsSumInvalid];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        var criteria = context.Items(context.Paths.EvaluationCriteria);
        var weights = criteria.Select(c => Values.TryGetNumber(JsonNavigator.Find(c, context.Paths.EvaluationWeight), out var w) ? w : (decimal?)null).ToArray();
        if (weights.Length == 0 || weights.Any(w => w is null))
        {
            return ValueTask.CompletedTask;
        }

        var sum = weights.Sum(w => w!.Value);
        if (Values.Money(sum) != 100m)
        {
            context.Report(FindingCodes.EvaluationWeightsSumInvalid, FindingSeverity.Error, RuleContext.PathOf(context.Paths.EvaluationCriteria),
                $"Die Gewichtungen der Bewertungskriterien ergeben zusammen {Values.FormatNumber(sum)} % statt 100 %.");
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Change request: cost item amount = quantity × rate; net cost = sum of cost item amounts;
/// revised value = original value + net cost.
/// </summary>
internal sealed class ChangeRequestArithmeticRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } =
    [
        FindingCodes.CrCostItemAmountMismatch, FindingCodes.CrCostSumMismatch, FindingCodes.CrRevisedValueMismatch,
    ];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        var p = context.Paths;
        var items = context.Items(p.CrCostItems);
        decimal? statedSum = items.Count == 0 ? null : 0m;
        decimal? derivedSum = items.Count == 0 ? null : 0m;
        foreach (var item in items)
        {
            var amountField = JsonNavigator.Find(item, p.CrCostItemAmount);
            var hasAmount = Values.TryGetNumber(amountField, out var amount);
            statedSum = hasAmount ? statedSum + Values.Money(amount) : null;

            if (Values.TryGetNumber(JsonNavigator.Find(item, p.CrCostItemQuantity), out var quantity)
                && Values.TryGetNumber(JsonNavigator.Find(item, p.CrCostItemRate), out var rate))
            {
                var expected = Values.Money(quantity * rate);
                var amountPath = amountField?.Path ?? $"{item.Path}.{p.CrCostItemAmount}";
                context.SetComputed(amountPath, expected);
                derivedSum += expected;
                if (hasAmount && Values.Money(amount) != expected)
                {
                    context.Report(FindingCodes.CrCostItemAmountMismatch, FindingSeverity.Error, amountPath,
                        $"Der Betrag {Values.FormatAmount(amount)} entspricht nicht Menge × Satz ({Values.FormatNumber(quantity)} × {Values.FormatAmount(rate)} = {Values.FormatAmount(expected)}).");
                }
            }
            else
            {
                derivedSum = hasAmount ? derivedSum + Values.Money(amount) : null;
            }
        }

        var costField = context.Find(p.CrNetCost);
        var costPath = costField?.Path ?? RuleContext.PathOf(p.CrNetCost);
        var hasCost = Values.TryGetNumber(costField, out var cost);
        if (derivedSum is { } derived)
        {
            context.SetComputed(costPath, derived);
        }

        if (statedSum is { } stated && hasCost && Values.Money(cost) != stated)
        {
            context.Report(FindingCodes.CrCostSumMismatch, FindingSeverity.Error, costPath,
                $"Die Nettoänderung {Values.FormatAmount(cost)} entspricht nicht der Summe der Kostenpositionen ({Values.FormatAmount(stated)}).");
        }

        if (hasCost && Values.TryGetNumber(context.Find(p.CrOriginalValue), out var original))
        {
            var expected = Values.Money(original + cost);
            var revisedField = context.Find(p.CrRevisedValue);
            var revisedPath = revisedField?.Path ?? RuleContext.PathOf(p.CrRevisedValue);
            context.SetComputed(revisedPath, expected);
            if (Values.TryGetNumber(revisedField, out var revised) && Values.Money(revised) != expected)
            {
                context.Report(FindingCodes.CrRevisedValueMismatch, FindingSeverity.Error, revisedPath,
                    $"Der neue Auftragswert {Values.FormatAmount(revised)} entspricht nicht ursprünglichem Wert plus Nettoänderung ({Values.FormatAmount(original)} + {Values.FormatAmount(cost)} = {Values.FormatAmount(expected)}).");
            }
        }

        return ValueTask.CompletedTask;
    }
}
