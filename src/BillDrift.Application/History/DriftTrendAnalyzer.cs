using BillDrift.Domain.Common;
using BillDrift.Domain.History;

namespace BillDrift.Application.History;

/// <summary>Aggregates drift index rows into recurring mismatch trends.</summary>
public sealed class DriftTrendAnalyzer
{
    /// <summary>Analyzes drift index entries for recurring mismatch patterns.</summary>
    public IReadOnlyList<DriftTrendEntry> Analyze(
        IReadOnlyList<DriftIndexEntry> entries,
        int minOccurrences = 2,
        MismatchType? mismatchTypeFilter = null,
        string? customerMexIdFilter = null)
    {
        var filtered = entries.AsEnumerable();
        if (mismatchTypeFilter is not null)
        {
            filtered = filtered.Where(e => e.MismatchType == mismatchTypeFilter);
        }

        if (customerMexIdFilter is not null)
        {
            filtered = filtered.Where(e =>
                e.CustomerMexId is not null &&
                string.Equals(e.CustomerMexId!.Value.Value, customerMexIdFilter, StringComparison.OrdinalIgnoreCase));
        }

        return filtered
            .GroupBy(e => e.StableKey)
            .Select(group =>
            {
                var ordered = group.OrderBy(e => e.CompletedAt).ToList();
                var first = ordered[0];
                var last = ordered[^1];
                var count = ordered.Count;
                return new DriftTrendEntry(
                    group.Key,
                    first.CustomerMexId,
                    first.CommercialKeyRoot,
                    first.MismatchType,
                    count,
                    first.RunId,
                    last.RunId,
                    first.CompletedAt,
                    last.CompletedAt,
                    count >= minOccurrences);
            })
            .Where(e => e.OccurrenceCount >= minOccurrences)
            .OrderByDescending(e => e.OccurrenceCount)
            .ThenByDescending(e => e.LastSeenAt)
            .ToList();
    }
}
