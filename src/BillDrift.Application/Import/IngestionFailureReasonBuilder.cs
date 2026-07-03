namespace BillDrift.Application.Import;

/// <summary>
/// Builds concise, operator-facing failure summaries from structured ingestion log entries.
/// </summary>
public static class IngestionFailureReasonBuilder
{
    private const int MaxLines = 5;

    /// <summary>
    /// Summarises error-level log entries into a multi-line reason string for API and UI display.
    /// </summary>
    public static string Build(
        IngestionOutcomeStatus status,
        IReadOnlyList<IngestionLogEntry> logEntries,
        string? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(logEntries);

        var errors = logEntries
            .Where(entry => entry.Severity == IngestionLogSeverity.Error)
            .ToList();

        if (errors.Count == 0)
        {
            return fallback ?? (status == IngestionOutcomeStatus.Failure
                ? "Ingestion failed with no diagnostic details."
                : string.Empty);
        }

        var grouped = errors
            .GroupBy(entry => entry.Reason)
            .OrderByDescending(group => group.Count())
            .ToList();

        var lines = new List<string>();
        var shown = 0;

        foreach (var group in grouped)
        {
            if (shown >= MaxLines)
            {
                break;
            }

            var first = group.First();
            var location = FormatLocation(first.Location);
            var countSuffix = group.Count() > 1 ? $" ({group.Count()} occurrences)" : string.Empty;
            var locationSuffix = location is null ? string.Empty : $" at {location}";
            lines.Add($"{group.Key}: {first.Message}{locationSuffix}{countSuffix}");
            shown++;
        }

        var remaining = grouped.Count - shown;
        if (remaining > 0)
        {
            lines.Add($"+ {remaining} more issue type(s).");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string? FormatLocation(IngestionLocation? location)
    {
        if (location is null)
        {
            return null;
        }

        return location.LineIndex.HasValue
            ? $"page {location.PageNumber}, block {location.BlockIndex}, line {location.LineIndex.Value}"
            : $"page {location.PageNumber}, block {location.BlockIndex}";
    }
}
