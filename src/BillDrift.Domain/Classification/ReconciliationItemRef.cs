using BillDrift.Domain.Common;

namespace BillDrift.Domain.Classification;

/// <summary>
/// Stable identity for a reconciliation item used for classification persistence across re-ingestion.
/// </summary>
public readonly record struct ReconciliationItemRef(
    ReconciliationItemKind Kind,
    string StableKey,
    Guid? EntityId,
    MexId CustomerMexId)
{
    private const int MaxStableKeyLength = 1024;

    /// <summary>
    /// Creates a validated item reference.
    /// </summary>
    public static ReconciliationItemRef Create(
        ReconciliationItemKind kind,
        string stableKey,
        MexId customerMexId,
        Guid? entityId = null)
    {
        if (string.IsNullOrWhiteSpace(stableKey))
        {
            throw new DomainValidationException(nameof(StableKey), "StableKey must be non-empty.");
        }

        if (stableKey.Length > MaxStableKeyLength)
        {
            throw new DomainValidationException(nameof(StableKey), $"StableKey must not exceed {MaxStableKeyLength} characters.");
        }

        return new ReconciliationItemRef(kind, stableKey, entityId, customerMexId);
    }
}
