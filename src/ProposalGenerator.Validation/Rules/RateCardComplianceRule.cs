using ProposalGenerator.Validation.Internal;
using ProposalGenerator.Validation.RateCards;

namespace ProposalGenerator.Validation.Rules;

/// <summary>
/// Daily rates must match the rate card that is valid on the document date, in the document currency.
/// SOW rate-card roles that are missing from the rate card are warnings (the agent must ask for the rate);
/// change-request cost items may also be non-role categories such as travel, so a missing category is info.
/// </summary>
internal sealed class RateCardComplianceRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } =
    [
        FindingCodes.RateCardUnverified, FindingCodes.RateCardNotValid, FindingCodes.RateCardCurrencyMismatch,
        FindingCodes.RoleNotInRateCard, FindingCodes.DailyRateMismatch,
    ];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        var p = context.Paths;
        var lines = Lines(context, p.RateCardLines, p.RateCardLineRole, p.RateCardLineDailyRate, FindingSeverity.Warning)
            .Concat(Lines(context, p.CrCostItems, p.CrCostItemCategory, p.CrCostItemRate, FindingSeverity.Info))
            .ToArray();
        if (lines.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        if (context.RateCards is not { Count: > 0 } rateCards)
        {
            context.Report(FindingCodes.RateCardUnverified, FindingSeverity.Info, lines[0].RatePath,
                "Die Tagessätze konnten nicht geprüft werden, weil keine Rate Card konfiguriert ist.");
            return ValueTask.CompletedTask;
        }

        var dateField = context.Find(p.DocumentDate);
        var datePath = dateField?.Path ?? RuleContext.PathOf(p.DocumentDate);
        if (!Values.TryGetDate(dateField, out var date))
        {
            context.Report(FindingCodes.RateCardUnverified, FindingSeverity.Info, datePath,
                "Die Tagessätze konnten nicht geprüft werden, weil das Dokumentdatum fehlt oder ungültig ist.");
            return ValueTask.CompletedTask;
        }

        var rateCard = rateCards.Where(r => r.IsValidOn(date)).OrderByDescending(r => r.ValidFrom).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
        if (rateCard is null)
        {
            var known = string.Join("; ", rateCards.OrderBy(r => r.ValidFrom).Select(r => $"{r.Id}: {Values.FormatDate(r.ValidFrom)}–{Values.FormatDate(r.ValidTo)}"));
            context.Report(FindingCodes.RateCardNotValid, FindingSeverity.Error, datePath,
                $"Für das Dokumentdatum {Values.FormatDate(date)} gibt es keine gültige Rate Card (bekannt: {known}). Bitte eine aktuelle Rate Card hinterlegen.");
            return ValueTask.CompletedTask;
        }

        if (Values.TryGetText(context.Find(p.PricingCurrency), out var currency) && currency != rateCard.Currency)
        {
            context.Report(FindingCodes.RateCardCurrencyMismatch, FindingSeverity.Error, RuleContext.PathOf(p.PricingCurrency),
                $"Die Dokumentwährung „{currency}“ weicht von der Währung der Rate Card {rateCard.Id} („{rateCard.Currency}“) ab; die Tagessätze können nicht verglichen werden.");
            return ValueTask.CompletedTask;
        }

        foreach (var line in lines)
        {
            var role = rateCard.FindRole(line.Role);
            if (role is null)
            {
                context.Report(FindingCodes.RoleNotInRateCard, line.UnknownRoleSeverity, line.RolePath,
                    $"Die Rolle „{line.Role}“ ist nicht in der Rate Card {rateCard.Id} ({rateCard.Source}) enthalten. Bitte den Tagessatz beim Nutzer erfragen, statt ihn zu schätzen.");
                continue;
            }

            context.SetComputed(line.RatePath, role.DailyRate);
            if (line.Rate is { } rate && Values.Money(rate) != Values.Money(role.DailyRate))
            {
                context.Report(FindingCodes.DailyRateMismatch, FindingSeverity.Error, line.RatePath,
                    $"Der Tagessatz {Values.FormatAmount(rate)} für „{role.Role}“ weicht von der Rate Card {rateCard.Id} ({rateCard.Source}) ab: {Values.FormatAmount(role.DailyRate)} {rateCard.Currency}.");
            }
        }

        return ValueTask.CompletedTask;
    }

    private static IEnumerable<RateLine> Lines(RuleContext context, string arrayPath, string rolePath, string ratePath, FindingSeverity unknownRoleSeverity)
    {
        foreach (var item in context.Items(arrayPath))
        {
            var roleField = JsonNavigator.Find(item, rolePath);
            var rateField = JsonNavigator.Find(item, ratePath);
            if (!Values.TryGetText(roleField, out var role) || rateField is null)
            {
                continue;
            }

            yield return new RateLine(role, roleField!.Value.Path, Values.TryGetNumber(rateField, out var rate) ? rate : null, rateField.Value.Path, unknownRoleSeverity);
        }
    }

    private sealed record RateLine(string Role, string RolePath, decimal? Rate, string RatePath, FindingSeverity UnknownRoleSeverity);
}
