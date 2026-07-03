using BillDrift.Domain.Approval;
using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.CatalogueReconciliation;

/// <summary>Maps catalogue proposed fixes into approval workflow proposals.</summary>
public sealed class CatalogueApprovalAdapter
{
    private static readonly MexId CatalogueScopeCustomer = MexId.Create("CATALOGUE");

    /// <summary>Converts actionable catalogue fixes to approval proposals.</summary>
    public IReadOnlyList<ApprovalProposal> ToApprovalProposals(
        CatalogueReconciliationRun run,
        string operatorId)
    {
        var runId = RunId.FromGuid(run.RunId.Value);
        var now = DateTimeOffset.UtcNow;
        var proposals = new List<ApprovalProposal>();

        foreach (var fix in run.ProposedFixes)
        {
            var exception = run.Exceptions.First(e => e.Id == fix.ExceptionId);
            var (category, eligibility, actionType) = MapEligibility(fix);
            var productLabel = exception.CommercialKeyRoot is { } root
                ? $"{root.OfferId.Value}/{root.SkuId.Value}"
                : exception.Description;

            proposals.Add(new ApprovalProposal(
                ApprovalProposalId.New(),
                runId,
                null,
                fix.IdempotencyKey,
                null,
                category,
                actionType,
                ApprovalDecisionState.Pending,
                eligibility,
                fix.Rationale,
                CatalogueScopeCustomer,
                productLabel,
                fix.CommercialKeyRoot,
                fix.PriorState,
                fix.ProposedState,
                10,
                [],
                null,
                now,
                null,
                false,
                operatorId,
                now));
        }

        return proposals;
    }

    private static (ApprovalProposalCategory, ApprovalEligibility, ProposedActionType?) MapEligibility(
        CatalogueProposedFix fix)
    {
        if (!fix.IsActionable)
        {
            return (ApprovalProposalCategory.Investigation, ApprovalEligibility.CatalogueConflict, null);
        }

        return (ApprovalProposalCategory.Catalogue, ApprovalEligibility.Eligible, ProposedActionType.CreateOrUpdateCatalogueEntry);
    }
}
