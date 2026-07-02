using BillDrift.Domain.Approval;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Approval;

/// <summary>Builds deterministic approved changeset exports from approved proposals.</summary>
public sealed class ApprovedChangesetBuilder
{
    /// <summary>Builds an ordered changeset from approved proposals.</summary>
    public ApprovedChangeset Build(
        RunId runId,
        IReadOnlyList<ApprovalProposal> approvedProposals,
        string exportedBy)
    {
        var entries = approvedProposals
            .Where(p => p.State == ApprovalDecisionState.Approved && p.ActionType is not null)
            .OrderBy(p => p.ExecutionOrder)
            .ThenBy(p => p.Category == ApprovalProposalCategory.Subscription ? 1 : 0)
            .ThenBy(p => p.Id.Value)
            .Select(p => new ApprovedChangesetEntry(
                p.Id,
                p.IdempotencyKey,
                p.ActionType!.Value,
                p.CustomerMexId,
                p.ProductLabel,
                p.PriorValues,
                p.ProposedValues,
                p.LastUpdatedAt,
                p.LastOperatorId ?? exportedBy,
                p.ExecutionOrder))
            .ToList();

        return new ApprovedChangeset(
            Guid.NewGuid(),
            runId,
            DateTimeOffset.UtcNow,
            exportedBy,
            entries,
            null);
    }
}
