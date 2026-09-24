namespace ProposalGenerator.Knowledge;

/// <summary>Formats an <see cref="IngestionReport"/> for the console.</summary>
public static class ReportFormatter
{
    /// <summary>Returns the report lines.</summary>
    public static IReadOnlyList<string> Format(IngestionReport report, string vectorStoreName)
    {
        var prefix = report.DryRun ? "[dry run] " : string.Empty;
        var store = report.VectorStoreId ?? "(does not exist yet)";
        var state = report.VectorStoreCreated ? (report.DryRun ? " (would be created)" : " (created)") : string.Empty;

        var lines = new List<string> { $"{prefix}Vector store '{vectorStoreName}': {store}{state}" };
        lines.AddRange(report.Entries.Select(e => $"{prefix}{e.Action,-9} {e.SourcePath}{(e.FileId is null ? string.Empty : "  " + e.FileId)}"));
        lines.Add($"{prefix}{report.Count(IngestionAction.Created)} created, {report.Count(IngestionAction.Updated)} updated, "
            + $"{report.Count(IngestionAction.Unchanged)} unchanged, {report.Count(IngestionAction.Removed)} removed, {report.Count(IngestionAction.Kept)} kept.");
        if (report.Count(IngestionAction.Kept) > 0)
        {
            lines.Add($"{prefix}Kept files no longer exist locally; run with --prune to remove them.");
        }

        return lines;
    }
}
