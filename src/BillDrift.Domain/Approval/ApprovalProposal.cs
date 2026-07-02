using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Domain.Approval;

/// <summary>
/// A human-reviewable snapshot of a reconciliation corrective action awaiting operator decision.
/// </summary>
public sealed record ApprovalProposal(
    ApprovalProposalId Id,
    RunId RunId,
    ProposedChangeId? ProposedChangeId,
    IdempotencyKey IdempotencyKey,
    MismatchId? MismatchId,
    ApprovalProposalCategory Category,
    ProposedActionType? ActionType,
    ApprovalDecisionState State,
    ApprovalEligibility Eligibility,
    string? EligibilityReason,
    MexId CustomerMexId,
    string ProductLabel,
    CommercialKeyRoot? CommercialKeyRoot,
    IReadOnlyDictionary<string, string> PriorValues,
    IReadOnlyDictionary<string, string> ProposedValues,
    int ExecutionOrder,
    IReadOnlyList<ApprovalProposalId> DependsOnProposalIds,
    ApprovalRiskIndicator? RiskIndicator,
    DateTimeOffset IngestedAt,
    RunId? SupersededByRunId,
    bool ApprovedWhileEligible,
    string? LastOperatorId,
    DateTimeOffset LastUpdatedAt);
