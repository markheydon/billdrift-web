using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Common;

namespace BillDrift.Application.Tests.Reconciliation;

public class CatalogueReconciliationTests
{
    [Fact]
    public void Catalogue_missing_proposes_CreateOrUpdateCatalogueEntry_when_enabled()
    {
        var engine = new ReconciliationEngine(new Mapping.ProductMappingResolver());
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load("catalogue-missing"),
            new ReconciliationOptions(ProposeCatalogueChanges: true)));

        Assert.Contains(run.Mismatches, m => m.Type == MismatchType.CatalogueMissing);
        Assert.Contains(run.ProposedChanges, p => p.ActionType == ProposedActionType.CreateOrUpdateCatalogueEntry);
    }

    [Fact]
    public void Catalogue_price_drift_detected_via_price_mismatch()
    {
        var engine = new ReconciliationEngine(new Mapping.ProductMappingResolver());
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load("price-mismatch"),
            new ReconciliationOptions(PriceTolerance: Money.Gbp(0))));

        Assert.Contains(run.Mismatches, m => m.Type == MismatchType.PriceMismatch);
    }
}
