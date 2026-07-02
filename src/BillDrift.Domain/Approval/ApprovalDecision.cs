using BillDrift.Domain.Reconciliation;

namespace BillDrift.Domain.Approval;

/// <summary>Immutable record of an operator approve or reject action.</summary>
public sealed record ApprovalDecision(
    ApprovalProposalId ProposalId,
    RunId RunId,
    ApprovalDecisionState PriorState,
    ApprovalDecisionState NewState,
    string OperatorId,
    DateTimeOffset DecidedAt,
    string? RejectionReason,
    bool AcknowledgedStale);
