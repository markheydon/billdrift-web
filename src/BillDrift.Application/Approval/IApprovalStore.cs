using BillDrift.Domain.Approval;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Approval;

/// <summary>Persistence abstraction for approval proposals, decisions, audit, and export metadata.</summary>
public interface IApprovalStore
{
    Task UpsertProposalAsync(ApprovalProposal proposal, CancellationToken cancellationToken = default);

    Task<ApprovalProposal?> GetProposalAsync(
        RunId runId,
        ApprovalProposalId proposalId,
        CancellationToken cancellationToken = default);

    Task<ApprovalProposal?> GetProposalByIdempotencyKeyAsync(
        RunId runId,
        IdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalProposal>> ListProposalsByRunAsync(
        RunId runId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalProposal>> ListProposalsByCustomerAsync(
        RunId runId,
        MexId customerMexId,
        CancellationToken cancellationToken = default);

    Task AppendDecisionAsync(ApprovalDecision decision, CancellationToken cancellationToken = default);

    Task AppendAuditEventAsync(ApprovalAuditEvent auditEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalAuditEvent>> ListAuditEventsAsync(
        RunId runId,
        ApprovalProposalId? proposalId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalProposal>> FindPriorProposalsAsync(
        MismatchId mismatchId,
        ProposedActionType? actionType,
        RunId currentRunId,
        CancellationToken cancellationToken = default);

    Task SaveExportMetadataAsync(
        Guid exportId,
        RunId runId,
        string exportedBy,
        string blobPath,
        int entryCount,
        string contentHash,
        CancellationToken cancellationToken = default);
}
