using BillDrift.Application.CatalogueReconciliation;
using BillDrift.Application.Mapping;
using BillDrift.Domain.Approval;

namespace BillDrift.Application.Tests.CatalogueReconciliation;

public class CatalogueApprovalAdapterTests
{
    [Fact]
    public void Actionable_fixes_map_to_eligible_catalogue_proposals()
    {
        var engine = new CatalogueReconciliationEngine(new ProductMappingResolver());
        var run = engine.Execute(CatalogueReconciliationTestDataBuilder.MissingPrice());
        var adapter = new CatalogueApprovalAdapter();

        var proposals = adapter.ToApprovalProposals(run, "operator@test");

        Assert.Contains(proposals, p =>
            p.Category == ApprovalProposalCategory.Catalogue &&
            p.Eligibility == ApprovalEligibility.Eligible);
    }

    [Fact]
    public void Manual_cleanup_maps_to_catalogue_conflict()
    {
        var engine = new CatalogueReconciliationEngine(new ProductMappingResolver());
        var run = engine.Execute(CatalogueReconciliationTestDataBuilder.DuplicateProducts());
        var adapter = new CatalogueApprovalAdapter();

        var proposals = adapter.ToApprovalProposals(run, "operator@test");

        Assert.All(proposals, p => Assert.Equal(ApprovalEligibility.CatalogueConflict, p.Eligibility));
    }
}
