using BillDrift.Application.Classification;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Approval;

/// <summary>Input for ingesting reconciliation proposals into the approval queue.</summary>
public sealed record ApprovalIngestionRequest(
    ReconciliationRun Run,
    ReconciliationExceptionViewModel Exceptions,
    ClassificationContext? Classifications,
    bool IncludeInvestigationItems = true);

/// <summary>Result of an ingest operation.</summary>
public sealed record ApprovalIngestionResult(
    RunId RunId,
    int IngestedCount,
    int PendingCount,
    int InvestigationCount,
    int SupersededCount);

/// <summary>Queue summary counts by state and category.</summary>
public sealed record ApprovalQueueSummary(
    int PendingCount,
    int ApprovedCount,
    int RejectedCount,
    int StaleCount,
    int InvestigationCount,
    int CatalogueCount,
    int SubscriptionCount);

/// <summary>Top-level approval queue for a reconciliation run.</summary>
public sealed record ApprovalQueueViewModel(
    RunId RunId,
    IReadOnlyList<ApprovalCustomerGroupViewModel> CustomerGroups,
    ApprovalQueueSummary Summary);

/// <summary>Per-customer grouping of proposals in the approval queue.</summary>
public sealed record ApprovalCustomerGroupViewModel(
    MexId CustomerMexId,
    string CustomerLabel,
    IReadOnlyList<ApprovalProposalViewModel> SubscriptionProposals,
    IReadOnlyList<ApprovalProposalViewModel> CatalogueProposals,
    IReadOnlyList<ApprovalProposalViewModel> InvestigationItems);

/// <summary>Operator-facing proposal row with action flags.</summary>
public sealed record ApprovalProposalViewModel(
    ApprovalProposalId Id,
    ApprovalProposalCategory Category,
    ProposedActionType? ActionType,
    ApprovalDecisionState State,
    ApprovalEligibility Eligibility,
    string? EligibilityReason,
    string ProductLabel,
    IReadOnlyDictionary<string, string> PriorValues,
    IReadOnlyDictionary<string, string> ProposedValues,
    int ExecutionOrder,
    ApprovalRiskIndicator? RiskIndicator,
    bool CanApprove,
    bool CanReject,
    bool CanExport,
    DateTimeOffset IngestedAt);

/// <summary>Optional filters when loading the approval queue.</summary>
public sealed record ApprovalQueueOptions(
    MexId? CustomerMexId = null,
    bool IncludeCatalogue = true,
    bool IncludeInvestigation = true);

/// <summary>Command to approve a single proposal.</summary>
public sealed record ApproveProposalCommand(
    RunId RunId,
    ApprovalProposalId ProposalId,
    bool AcknowledgeStale = false);

/// <summary>Command to reject a single proposal.</summary>
public sealed record RejectProposalCommand(
    RunId RunId,
    ApprovalProposalId ProposalId,
    string Reason);

/// <summary>Preview result for bulk approve confirmation.</summary>
public sealed record BulkApprovePreview(
    string ConfirmationToken,
    int Count,
    int SubscriptionActions,
    int CatalogueActions);

/// <summary>Command to bulk approve eligible pending proposals.</summary>
public sealed record BulkApproveCommand(
    RunId RunId,
    MexId CustomerMexId,
    IReadOnlyList<ApprovalProposalId> ProposalIds,
    string ConfirmationToken);

/// <summary>Result of a bulk approve operation.</summary>
public sealed record BulkApproveResult(
    IReadOnlyList<ApprovalDecision> Decisions);

/// <summary>Command to export approved changes to blob storage.</summary>
public sealed record ExportChangesetCommand(
    RunId RunId,
    MexId? CustomerMexId = null);
