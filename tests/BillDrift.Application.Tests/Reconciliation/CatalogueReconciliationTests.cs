using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Common;
using FluentAssertions;

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

        run.Mismatches.Should().Contain(m => m.Type == MismatchType.CatalogueMissing);
        run.ProposedChanges.Should().Contain(p => p.ActionType == ProposedActionType.CreateOrUpdateCatalogueEntry);
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

        run.Mismatches.Should().Contain(m => m.Type == MismatchType.PriceMismatch);
    }
}
