using BillDrift.Domain.Reconciliation;

namespace BillDrift.Domain.History;

/// <summary>Append-only audit event for run history operations.</summary>
public sealed record RunHistoryAuditEvent(
    Guid EventId,
    RunHistoryAuditEventType EventType,
    RunId RunId,
    DateTimeOffset Timestamp,
    string Summary,
    string? OperatorId = null,
    string? PayloadJson = null);
