using ProposalGenerator.Validation.Internal;

namespace ProposalGenerator.Validation.Rules;

/// <summary>The project period must not end before it starts. Equal dates are valid (one-day engagement).</summary>
internal sealed class ProjectPeriodRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } = [FindingCodes.ProjectPeriodInvalid];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        var start = context.Find(context.Paths.ProjectStartDate);
        var end = context.Find(context.Paths.ProjectEndDate);
        if (Values.TryGetDate(start, out var startDate) && Values.TryGetDate(end, out var endDate) && endDate < startDate)
        {
            context.Report(FindingCodes.ProjectPeriodInvalid, FindingSeverity.Error, end!.Value.Path,
                $"Das Enddatum ({Values.FormatDate(endDate)}) liegt vor dem Startdatum ({Values.FormatDate(startDate)}).");
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Dates that follow each other in a tender or change process must be in order, for example
/// issue date ≤ questions deadline ≤ submission deadline. Equal dates are valid.
/// </summary>
internal sealed class DeadlineOrderRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } = [FindingCodes.DeadlineOrderInvalid];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        var p = context.Paths;
        (string Earlier, string EarlierName, string Later, string LaterName)[] pairs =
        [
            (p.DocumentDate, "Ausgabedatum", p.RfpQuestionsDeadline, "Frist für Rückfragen"),
            (p.RfpQuestionsDeadline, "Frist für Rückfragen", p.RfpSubmissionDeadline, "Abgabefrist"),
            (p.DocumentDate, "Ausgabedatum", p.RfpSubmissionDeadline, "Abgabefrist"),
            (p.DocumentDate, "Ausgabedatum", p.RfiQuestionsDeadline, "Frist für Rückfragen"),
            (p.RfiQuestionsDeadline, "Frist für Rückfragen", p.RfiResponseDeadline, "Antwortfrist"),
            (p.DocumentDate, "Ausgabedatum", p.RfiResponseDeadline, "Antwortfrist"),
            (p.DocumentDate, "Datum des Change Requests", p.CrDecisionDue, "gewünschte Entscheidung"),
        ];

        foreach (var (earlierPath, earlierName, laterPath, laterName) in pairs)
        {
            var later = context.Find(laterPath);
            if (Values.TryGetDate(context.Find(earlierPath), out var earlierDate)
                && Values.TryGetDate(later, out var laterDate)
                && laterDate < earlierDate)
            {
                context.Report(FindingCodes.DeadlineOrderInvalid, FindingSeverity.Error, later!.Value.Path,
                    $"Die {laterName} ({Values.FormatDate(laterDate)}) liegt vor dem Termin „{earlierName}“ ({Values.FormatDate(earlierDate)}).");
            }
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Milestones and deliverable due dates should lie within the project period, bounds included.
/// The rule is skipped when the period itself is inverted, because <see cref="ProjectPeriodRule"/> reports that.
/// </summary>
internal sealed class WithinProjectPeriodRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } = [FindingCodes.MilestoneOutsidePeriod, FindingCodes.DeliverableOutsidePeriod];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        var p = context.Paths;
        DateOnly? start = Values.TryGetDate(context.Find(p.ProjectStartDate), out var s) ? s : null;
        DateOnly? end = Values.TryGetDate(context.Find(p.ProjectEndDate), out var e) ? e : null;
        if ((start is null && end is null) || (start is not null && end is not null && end < start))
        {
            return ValueTask.CompletedTask;
        }

        Check(context, p.Milestones, p.MilestoneDate, start, end, FindingCodes.MilestoneOutsidePeriod, "Der Meilenstein");
        Check(context, p.Deliverables, p.DeliverableDueDate, start, end, FindingCodes.DeliverableOutsidePeriod, "Das Fälligkeitsdatum des Liefergegenstands");
        return ValueTask.CompletedTask;
    }

    private static void Check(RuleContext context, string arrayPath, string datePath, DateOnly? start, DateOnly? end, string code, string subject)
    {
        foreach (var item in context.Items(arrayPath))
        {
            var located = JsonNavigator.Find(item, datePath);
            if (!Values.TryGetDate(located, out var date))
            {
                continue;
            }

            var name = Values.TryGetText(JsonNavigator.Find(item, context.Paths.ItemName), out var text) ? $" „{text}“" : string.Empty;
            if (start is { } first && date < first)
            {
                context.Report(code, FindingSeverity.Warning, located!.Value.Path,
                    $"{subject}{name} ({Values.FormatDate(date)}) liegt vor dem Projektstart ({Values.FormatDate(first)}).");
            }
            else if (end is { } last && date > last)
            {
                context.Report(code, FindingSeverity.Warning, located!.Value.Path,
                    $"{subject}{name} ({Values.FormatDate(date)}) liegt nach dem Projektende ({Values.FormatDate(last)}).");
            }
        }
    }
}
