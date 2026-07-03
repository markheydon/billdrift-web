using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Domain.History;

/// <summary>Summary metrics denormalized for run list views.</summary>
public sealed record RunSummaryMetrics(
    int MatchGroupCount,
    int MismatchCount,
    IReadOnlyDictionary<string, int> MismatchCountByCategory,
    int ProposedChangeCount,
    bool CleanRun);

/// <summary>Immutable archived reconciliation run record.</summary>
public sealed record ReconciliationRunRecord(
    RunId RunId,
    RunArchiveStatus Status,
    BillingPeriod BillingPeriodScope,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? InitiatorId,
    MappingVersionReference MappingVersion,
    IReadOnlyList<InputSnapshotMetadata> InputSnapshots,
    RunSummaryMetrics SummaryMetrics,
    string ManifestBlobPath,
    string? FailureReason = null,
    bool IsArchived = false,
    DateTimeOffset? ArchivedAt = null,
    DateTimeOffset? RetentionExpiresAt = null);
