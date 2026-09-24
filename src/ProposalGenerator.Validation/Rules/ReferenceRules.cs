using System.Text.Json;
using ProposalGenerator.Validation.Internal;
using ProposalGenerator.Validation.Registry;

namespace ProposalGenerator.Validation.Rules;

/// <summary>A reference from a document to its predecessor, for example <c>msa.reference</c> in a SOW.</summary>
internal sealed record PredecessorReference(DocumentType Type, string Number, string NumberPath, string? Version, string? VersionPath)
{
    public static IReadOnlyList<PredecessorReference> Of(RuleContext context)
    {
        var p = context.Paths;
        (DocumentType Type, string NumberPath, string? VersionPath)[] candidates = context.DocumentType switch
        {
            DocumentType.Sow => [(DocumentType.Msa, p.MsaReference, p.MsaVersion)],
            DocumentType.ChangeRequest => [(DocumentType.Sow, p.SowReference, p.SowVersion), (DocumentType.Msa, p.MsaReference, p.MsaVersion)],
            DocumentType.Rfp => [(DocumentType.Rfi, p.RfpRfiReference, null), (DocumentType.Msa, p.MsaReference, p.MsaVersion)],
            _ => [],
        };

        var references = new List<PredecessorReference>();
        foreach (var (type, numberPath, versionPath) in candidates)
        {
            var number = context.Find(numberPath);
            if (!Values.TryGetText(number, out var numberText))
            {
                continue;
            }

            var version = versionPath is null ? null : context.Find(versionPath);
            references.Add(new PredecessorReference(
                type,
                numberText,
                number!.Value.Path,
                Values.TryGetText(version, out var versionText) ? versionText : null,
                version?.Path));
        }

        return references;
    }

    public string DisplayType => Type switch
    {
        DocumentType.ChangeRequest => "Change Request",
        _ => Type.ToName().ToUpperInvariant(),
    };
}

/// <summary>
/// Each predecessor reference (for example <c>msa.reference</c>) must match the entry of the same type in
/// <c>document.references</c>, including the version when both state one.
/// </summary>
internal sealed class ReferenceConsistencyRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } = [FindingCodes.ReferenceMismatch, FindingCodes.ReferenceNotListed];

    public ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        var p = context.Paths;
        var listed = context.Items(p.DocumentReferences)
            .Select(item => (
                Type: Values.TryGetText(JsonNavigator.Find(item, p.ReferenceType), out var t) && DocumentTypeNames.TryParse(t, out var parsed) ? parsed : null,
                Number: Values.TryGetText(JsonNavigator.Find(item, p.ReferenceNumber), out var n) ? n : null,
                Version: Values.TryGetText(JsonNavigator.Find(item, p.ReferenceVersion), out var v) ? v : null))
            .ToArray();

        foreach (var reference in PredecessorReference.Of(context))
        {
            var sameType = listed.Where(l => l.Type == reference.Type).ToArray();
            if (sameType.Length == 0)
            {
                context.Report(FindingCodes.ReferenceNotListed, FindingSeverity.Warning, reference.NumberPath,
                    $"Das Bezugsdokument {reference.DisplayType} „{reference.Number}“ fehlt in der Liste der Bezugsdokumente (document.references).");
                continue;
            }

            var match = sameType.FirstOrDefault(l => l.Number == reference.Number);
            if (match.Number is null)
            {
                context.Report(FindingCodes.ReferenceMismatch, FindingSeverity.Error, reference.NumberPath,
                    $"Die Referenz „{reference.Number}“ stimmt nicht mit dem {reference.DisplayType} in der Liste der Bezugsdokumente überein ({string.Join(", ", sameType.Select(l => $"„{l.Number}“"))}).");
            }
            else if (reference.Version is not null && match.Version is not null && reference.Version != match.Version)
            {
                context.Report(FindingCodes.ReferenceMismatch, FindingSeverity.Error, reference.VersionPath!,
                    $"Die Version „{reference.Version}“ des {reference.DisplayType} „{reference.Number}“ stimmt nicht mit der Liste der Bezugsdokumente überein (Version „{match.Version}“).");
            }
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Predecessor documents must exist in the configured registry (in the referenced version); party names must
/// equal the predecessor's; a change request's original value must equal the amended SOW's total price.
/// </summary>
internal sealed class PredecessorRule : IPlausibilityRule
{
    public IReadOnlyCollection<string> Codes { get; } =
    [
        FindingCodes.PredecessorUnverified, FindingCodes.PredecessorNotFound, FindingCodes.PredecessorVersionNotFound,
        FindingCodes.PartyNameMismatch, FindingCodes.CrOriginalValueMismatch,
    ];

    public async ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken)
    {
        foreach (var reference in PredecessorReference.Of(context))
        {
            if (context.Registry is null)
            {
                context.Report(FindingCodes.PredecessorUnverified, FindingSeverity.Info, reference.NumberPath,
                    $"Das Bezugsdokument {reference.DisplayType} „{reference.Number}“ konnte nicht geprüft werden, weil kein Dokumentenregister konfiguriert ist.");
                continue;
            }

            var versions = await context.Registry.FindAsync(reference.Type, reference.Number, cancellationToken).ConfigureAwait(false);
            if (versions.Count == 0)
            {
                context.Report(FindingCodes.PredecessorNotFound, FindingSeverity.Error, reference.NumberPath,
                    $"Das Bezugsdokument {reference.DisplayType} „{reference.Number}“ existiert nicht.");
                continue;
            }

            RegisteredDocument predecessor;
            if (reference.Version is not null)
            {
                var match = versions.FirstOrDefault(v => v.Version == reference.Version);
                if (match is null)
                {
                    context.Report(FindingCodes.PredecessorVersionNotFound, FindingSeverity.Error, reference.VersionPath!,
                        $"Das Bezugsdokument {reference.DisplayType} „{reference.Number}“ existiert nicht in Version „{reference.Version}“ (vorhanden: {string.Join(", ", versions.Select(v => v.Version).Order(VersionComparer.Instance))}).");
                    continue;
                }

                predecessor = match;
            }
            else
            {
                predecessor = versions.OrderByDescending(v => v.Version, VersionComparer.Instance).First();
            }

            CheckPartyName(context, context.Paths.SupplierName, "Lieferanten", reference, predecessor);
            CheckPartyName(context, context.Paths.CustomerName, "Kunden", reference, predecessor);

            if (context.DocumentType == DocumentType.ChangeRequest && reference.Type == DocumentType.Sow)
            {
                CheckOriginalValue(context, reference, predecessor);
            }
        }
    }

    private static void CheckPartyName(RuleContext context, string namePath, string party, PredecessorReference reference, RegisteredDocument predecessor)
    {
        var own = context.Find(namePath);
        if (!Values.TryGetText(own, out var ownName)
            || !Values.TryGetText(JsonNavigator.Find(predecessor.Content, JsonNavigator.RootPath, namePath), out var predecessorName))
        {
            return;
        }

        if (Normalize(ownName) != Normalize(predecessorName))
        {
            context.Report(FindingCodes.PartyNameMismatch, FindingSeverity.Error, own!.Value.Path,
                $"Der Name des {party} „{ownName}“ weicht vom Bezugsdokument {reference.DisplayType} „{predecessor.Number}“ Version {predecessor.Version} ab („{predecessorName}“).");
        }
    }

    private static void CheckOriginalValue(RuleContext context, PredecessorReference reference, RegisteredDocument sow)
    {
        var p = context.Paths;
        if (!Values.TryGetNumber(JsonNavigator.Find(sow.Content, JsonNavigator.RootPath, p.PricingTotal), out var sowTotal))
        {
            return;
        }

        var originalField = context.Find(p.CrOriginalValue);
        var originalPath = originalField?.Path ?? RuleContext.PathOf(p.CrOriginalValue);
        context.SetComputed(originalPath, Values.Money(sowTotal));
        if (Values.TryGetNumber(originalField, out var original) && Values.Money(original) != Values.Money(sowTotal))
        {
            context.Report(FindingCodes.CrOriginalValueMismatch, FindingSeverity.Error, originalPath,
                $"Der ursprüngliche Auftragswert {Values.FormatAmount(original)} entspricht nicht dem Gesamtpreis des {reference.DisplayType} „{sow.Number}“ Version {sow.Version} ({Values.FormatAmount(sowTotal)}).");
        }
    }

    private static string Normalize(string name) => string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>Orders versions such as <c>1.2</c> and <c>1.10</c> numerically; non-numeric parts compare ordinally.</summary>
internal sealed class VersionComparer : IComparer<string>
{
    public static VersionComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        var left = (x ?? string.Empty).Split('.');
        var right = (y ?? string.Empty).Split('.');
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            var a = i < left.Length ? left[i] : "0";
            var b = i < right.Length ? right[i] : "0";
            var result = int.TryParse(a, out var na) && int.TryParse(b, out var nb)
                ? na.CompareTo(nb)
                : string.CompareOrdinal(a, b);
            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }
}
