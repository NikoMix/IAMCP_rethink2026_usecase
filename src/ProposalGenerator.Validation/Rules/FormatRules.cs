using System.Text.Json;
using ProposalGenerator.Validation.Internal;

namespace ProposalGenerator.Validation.Rules;

/// <summary>The fields each format rule inspects, derived from the configured paths.</summary>
internal static class FieldSets
{
    public static IEnumerable<Located> Dates(RuleContext context)
    {
        var p = context.Paths;
        return Single(context, p.DocumentDate, p.ProjectStartDate, p.ProjectEndDate, p.MsaDate, p.SowDate,
                p.RfpQuestionsDeadline, p.RfpSubmissionDeadline, p.RfiQuestionsDeadline, p.RfiResponseDeadline, p.CrDecisionDue)
            .Concat(ItemFields(context, (p.DocumentReferences, p.ReferenceDate), (p.Milestones, p.MilestoneDate),
                (p.Deliverables, p.DeliverableDueDate), (p.RfpDeliverables, p.DeliverableDueDate), (p.PaymentSchedule, p.PaymentDate),
                (p.RfpTimeline, p.TimelineDate), (p.RfiTimeline, p.TimelineDate),
                (p.CrImpactMilestones, p.CrMilestoneCurrentDate), (p.CrImpactMilestones, p.CrMilestoneRevisedDate)));
    }

    /// <summary>Amounts, quantities, rates and weights that must not be negative.</summary>
    public static IEnumerable<Located> NonNegativeNumbers(RuleContext context)
    {
        var p = context.Paths;
        return Single(context, p.PricingTotal, p.PricingCap, p.CrOriginalValue, p.CrRevisedValue, p.LiabilityCapAmount)
            .Concat(ItemFields(context, (p.RateCardLines, p.RateCardLineDailyRate), (p.RateCardLines, p.RateCardLineDays),
                (p.RateCardLines, p.RateCardLineSubtotal), (p.PaymentSchedule, p.PaymentAmount), (p.PaymentSchedule, p.PaymentPercentage),
                (p.EvaluationCriteria, p.EvaluationWeight), (p.CrCostItems, p.CrCostItemRate)));
    }

    /// <summary>Numbers that may be negative, such as the net change of a change request.</summary>
    public static IEnumerable<Located> SignedNumbers(RuleContext context)
    {
        var p = context.Paths;
        return Single(context, p.CrNetCost)
            .Concat(ItemFields(context, (p.CrCostItems, p.CrCostItemQuantity), (p.CrCostItems, p.CrCostItemAmount)));
    }

    private static IEnumerable<Located> Single(RuleContext context, params string[] paths) =>
        paths.Select(context.Find).OfType<Located>();

    private static IEnumerable<Located> ItemFields(RuleContext context, params (string Array, string Field)[] fields) =>
        fields.SelectMany(field => context.Items(field.Array)
            .Select(item => JsonNavigator.Find(item, field.Field))
            .OfType<Located>());

    public static string Raw(JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
        return text.Length <= 60 ? text : string.Concat(text.AsSpan(0, 57), "...");
    }
}

/// <summary><c>document.type</c> must match the requested document type.</summary>
internal sealed class DocumentTypeRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } = [FindingCodes.DocumentTypeMismatch];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        if (context.Find(context.Paths.DocumentType) is { } located
            && (located.Value.ValueKind != JsonValueKind.String || located.Value.GetString() != context.DocumentType.ToName()))
        {
            context.Report(FindingCodes.DocumentTypeMismatch, FindingSeverity.Error, located.Path,
                $"Der Dokumenttyp im Dokument („{FieldSets.Raw(located.Value)}“) passt nicht zum geprüften Dokumenttyp „{context.DocumentType.ToName()}“.");
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>Date fields must be <c>YYYY-MM-DD</c> calendar dates.</summary>
internal sealed class DateFormatRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } = [FindingCodes.DateInvalid];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        foreach (var date in FieldSets.Dates(context))
        {
            if (!Values.TryGetDate(date, out _))
            {
                context.Report(FindingCodes.DateInvalid, FindingSeverity.Error, date.Path,
                    $"Das Datum „{FieldSets.Raw(date.Value)}“ ist ungültig. Erwartet wird ein Kalenderdatum im Format JJJJ-MM-TT.");
            }
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>Numeric fields must be JSON numbers that fit a decimal.</summary>
internal sealed class NumberFormatRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } = [FindingCodes.NumberInvalid];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        foreach (var number in FieldSets.NonNegativeNumbers(context).Concat(FieldSets.SignedNumbers(context)))
        {
            if (!Values.TryGetNumber(number, out _))
            {
                context.Report(FindingCodes.NumberInvalid, FindingSeverity.Error, number.Path,
                    $"Der Wert „{FieldSets.Raw(number.Value)}“ ist keine gültige Zahl. Beträge und Mengen werden als Zahl ohne Währung und Einheit angegeben.");
            }
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>Amounts, quantities, rates and weights must not be negative (the net change of a change request may be).</summary>
internal sealed class NonNegativeRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } = [FindingCodes.NegativeValue];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        foreach (var number in FieldSets.NonNegativeNumbers(context))
        {
            if (Values.TryGetNumber(number, out var value) && value < 0)
            {
                context.Report(FindingCodes.NegativeValue, FindingSeverity.Error, number.Path,
                    $"Der Wert {Values.FormatNumber(value)} darf nicht negativ sein.");
            }
        }

        return ValueTask.CompletedTask;
    }
}
