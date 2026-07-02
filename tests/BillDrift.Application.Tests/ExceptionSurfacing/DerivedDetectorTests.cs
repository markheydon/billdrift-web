using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using FluentAssertions;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

public class DerivedDetectorTests
{
    private readonly ExceptionSurfacingTestBuilder _builder = new();

    [Fact]
    public void Orphaned_stripe_item_produces_orphaned_billing_category()
    {
        var vm = _builder.SurfaceScenario("orphaned-stripe-item");

        vm.FlatExceptions().Should().Contain(e =>
            e.Category == ExceptionCategory.OrphanedBillingItem &&
            e.Domain == ReconciliationDomain.TruthVsStripe);
    }

    [Fact]
    public void Mex_id_mismatch_produces_mex_id_category()
    {
        var vm = _builder.SurfaceScenario("mex-id-mismatch");

        vm.FlatExceptions().Should().Contain(e =>
            e.Category == ExceptionCategory.MexIdMismatch &&
            e.Domain == ReconciliationDomain.SupplierCostVsMapping);
    }

    [Fact]
    public void Orphaned_exception_includes_stripe_evidence()
    {
        var vm = _builder.SurfaceScenario("orphaned-stripe-item");

        var orphan = vm.FlatExceptions().Single(e => e.Category == ExceptionCategory.OrphanedBillingItem);
        orphan.Evidence.Should().Contain(e => e.Source == EvidenceSource.StripeSubscriptionItem);
    }
}
