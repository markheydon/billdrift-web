using BillDrift.Application.Approval;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.Approval;

public sealed class ApprovalEligibilityEvaluatorTests
{
    private readonly ApprovalEligibilityEvaluator _evaluator = new();

    [Fact]
    public void Mapping_ambiguous_investigation_is_ineligible_per_quickstart_V4()
    {
        var evaluation = _evaluator.EvaluateInvestigation(CreateInvestigationException());

        evaluation.Eligibility.Should().Be(ApprovalEligibility.InvestigationOnly);
    }

    [Fact]
    public void Catalogue_conflict_blocks_approval()
    {
        var proposedChange = CreateCatalogueChange();
        var evaluation = _evaluator.EvaluateProposedChange(
            proposedChange,
            null,
            null,
            null,
            null,
            [
                CreateCatalogueProposal(),
                CreateCatalogueProposal()
            ]);

        evaluation.Eligibility.Should().Be(ApprovalEligibility.CatalogueConflict);
    }

    [Fact]
    public void Single_existing_catalogue_proposal_with_same_key_is_a_conflict()
    {
        // The proposal under evaluation is not part of the existing set, so a single prior
        // catalogue proposal sharing the commercial key already constitutes a conflict.
        var proposedChange = CreateCatalogueChange();
        var evaluation = _evaluator.EvaluateProposedChange(
            proposedChange,
            null,
            null,
            null,
            null,
            [CreateCatalogueProposal()]);

        evaluation.Eligibility.Should().Be(ApprovalEligibility.CatalogueConflict);
    }

    private static SurfacedException CreateInvestigationException() =>
        new(
            SurfacedExceptionId.FromDerived(RunId.New(), "mapping", "item-1"),
            ExceptionCategory.OfferSkuAmbiguousMapping,
            ReconciliationDomain.SupplierCostVsMapping,
            ExceptionSeverity.Warning,
            CustomerIdentity.Create(MexId.Create("MEX-001"), "Customer"),
            null,
            "Ambiguous mapping",
            [],
            RequiresActionNow: false,
            null,
            0,
            [],
            null,
            null);

    private static ProposedChange CreateCatalogueChange() =>
        new(
            ProposedChangeId.New(),
            IdempotencyKey.Create(RunId.New(), MismatchId.New(), ProposedActionType.CreateOrUpdateCatalogueEntry),
            MismatchId.New(),
            ProposedActionType.CreateOrUpdateCatalogueEntry,
            new ProposedChangeTarget(),
            new Dictionary<string, string> { ["name"] = "Product" },
            new CatalogueEntryPayload(
                null,
                "Product",
                CommercialKeyRoot.Create(OfferId.Create("OFFER"), SkuId.Create("SKU")),
                []),
            10);

    private static ApprovalProposal CreateCatalogueProposal() =>
        new(
            ApprovalProposalId.New(),
            RunId.New(),
            null,
            new IdempotencyKey("key"),
            null,
            ApprovalProposalCategory.Catalogue,
            ProposedActionType.CreateOrUpdateCatalogueEntry,
            ApprovalDecisionState.Pending,
            ApprovalEligibility.Eligible,
            null,
            MexId.Create("MEX-001"),
            "Product",
            CommercialKeyRoot.Create(OfferId.Create("OFFER"), SkuId.Create("SKU")),
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            10,
            [],
            null,
            DateTimeOffset.UtcNow,
            null,
            false,
            null,
            DateTimeOffset.UtcNow);
}
