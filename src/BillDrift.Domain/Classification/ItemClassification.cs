namespace BillDrift.Domain.Classification;

/// <summary>
/// Classification result for a single reconciliation item.
/// </summary>
public sealed record ItemClassification(
    ReconciliationItemRef ItemRef,
    ReconciliationItemClassification Classification,
    ClassificationSource Source,
    string RuleBasis,
    ClassificationConfidence Confidence,
    string? OverrideNotes,
    DateTimeOffset ClassifiedAt,
    string? OperatorId);

/// <summary>
/// Operator override persisted for a reconciliation item.
/// </summary>
public sealed record ClassificationOverride(
    ReconciliationItemRef ItemRef,
    ReconciliationItemClassification Classification,
    string Notes,
    string OperatorId,
    DateTimeOffset CreatedAt);

/// <summary>
/// Append-only audit entry for classification changes.
/// </summary>
public sealed record ClassificationHistoryEntry(
    ReconciliationItemRef ItemRef,
    ReconciliationItemClassification? PriorClassification,
    ReconciliationItemClassification NewClassification,
    ClassificationSource Source,
    string? Notes,
    string? OperatorId,
    DateTimeOffset Timestamp);
