using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Domain.Approval;

/// <summary>Append-only audit event for the approval workflow.</summary>
public sealed record ApprovalAuditEvent(
    Guid EventId,
    ApprovalAuditEventType EventType,
    RunId RunId,
    ApprovalProposalId? ProposalId,
    string? OperatorId,
    DateTimeOffset Timestamp,
    string Summary,
    string? PayloadJson);

/// <summary>Deterministic export artifact containing only operator-approved actions.</summary>
public sealed record ApprovedChangeset(
    Guid ExportId,
    RunId RunId,
    DateTimeOffset ExportedAt,
    string ExportedBy,
    IReadOnlyList<ApprovedChangesetEntry> Entries,
    string? BlobUri);

/// <summary>Single approved action included in an export changeset.</summary>
public sealed record ApprovedChangesetEntry(
    ApprovalProposalId ProposalId,
    IdempotencyKey IdempotencyKey,
    ProposedActionType ActionType,
    MexId CustomerMexId,
    string ProductLabel,
    IReadOnlyDictionary<string, string> PriorValues,
    IReadOnlyDictionary<string, string> ProposedValues,
    DateTimeOffset ApprovedAt,
    string ApprovedBy,
    int ExecutionOrder);
