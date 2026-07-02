using BillDrift.Domain.Classification;

namespace BillDrift.Application.Classification;

/// <summary>
/// Immutable snapshot of all item classifications for a reconciliation run.
/// </summary>
public sealed record ClassificationContext(
    IReadOnlyDictionary<string, ItemClassification> ByStableKey,
    DateTimeOffset ClassifiedAt)
{
    /// <summary>Looks up classification by item reference.</summary>
    public ItemClassification? Get(ReconciliationItemRef itemRef) =>
        ByStableKey.TryGetValue(itemRef.StableKey, out var classification) ? classification : null;

    /// <summary>Attempts lookup by stable key string.</summary>
    public bool TryGet(string stableKey, out ItemClassification? classification) =>
        ByStableKey.TryGetValue(stableKey, out classification);
}
