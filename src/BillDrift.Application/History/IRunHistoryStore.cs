using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.History;

/// <summary>Persistence abstraction for run history table storage.</summary>
public interface IRunHistoryStore
{
    Task UpsertRunAsync(ReconciliationRunRecord record, CancellationToken cancellationToken = default);

    Task<ReconciliationRunRecord?> GetRunAsync(RunId runId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ReconciliationRunRecord> Items, string? ContinuationToken)> ListRunsAsync(
        RunHistoryListFilter filter,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken = default);

    Task UpsertInputMetadataAsync(
        RunId runId,
        IReadOnlyList<InputSnapshotMetadata> snapshots,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InputSnapshotMetadata>> GetInputMetadataAsync(
        RunId runId,
        CancellationToken cancellationToken = default);

    Task UpsertDriftIndexRowsAsync(
        RunId runId,
        DateTimeOffset completedAt,
        IReadOnlyList<DriftIndexEntry> rows,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriftIndexEntry>> QueryDriftIndexAsync(
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        CancellationToken cancellationToken = default);

    Task AppendAuditEventAsync(RunHistoryAuditEvent auditEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RunHistoryAuditEvent>> ListAuditEventsAsync(
        RunId runId,
        CancellationToken cancellationToken = default);
}

/// <summary>Denormalized drift index row for trend queries.</summary>
public sealed record DriftIndexEntry(
    StableMismatchKey StableKey,
    RunId RunId,
    MexId? CustomerMexId,
    CommercialKeyRoot? CommercialKeyRoot,
    MismatchType MismatchType,
    MismatchSeverity Severity,
    MismatchId MismatchId,
    DateTimeOffset CompletedAt,
    string DescriptionSummary);
