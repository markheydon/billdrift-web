using BillDrift.Domain.Approval;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Domain.History;

/// <summary>Approval status link joined from feature 007 at read time.</summary>
public sealed record ProposalStatusLink(
    ProposedChangeId ProposedChangeId,
    IdempotencyKey IdempotencyKey,
    ApprovalDecisionState DecisionState,
    string? DecidedBy = null,
    DateTimeOffset? DecidedAt = null,
    string? RejectionReason = null,
    RunId? SupersededByRunId = null);

/// <summary>Future write-back execution outcome placeholder.</summary>
public sealed record ExecutionOutcome(
    ProposedChangeId ProposedChangeId,
    ExecutionOutcomeStatus Status,
    DateTimeOffset? ExecutedAt = null,
    string? OutcomeSummary = null,
    string? ErrorDetail = null);
