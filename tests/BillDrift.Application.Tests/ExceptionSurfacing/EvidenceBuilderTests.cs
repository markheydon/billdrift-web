using BillDrift.Application.Reconciliation;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Common;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

public class EvidenceBuilderTests
{
    private readonly ExceptionSurfacingTestBuilder _builder = new();

    [Fact]
    public void Quantity_mismatch_includes_subscription_truth_and_stripe_sources()
    {
        var vm = _builder.SurfaceScenario("quantity-mismatch", new ReconciliationOptions(PriceTolerance: Money.Gbp(0)));

        var exception = vm.FlatExceptions().Single(e => e.Category == ExceptionCategory.QuantityLicenceMismatch);
        Assert.Contains(EvidenceSource.SubscriptionTruth, exception.Evidence.Select(e => e.Source));
        Assert.Contains(EvidenceSource.StripeSubscriptionItem, exception.Evidence.Select(e => e.Source));
        Assert.Contains("10", exception.Explanation);
        Assert.Contains("5", exception.Explanation);
    }

    [Fact]
    public void Mapping_ambiguous_lists_candidate_evidence()
    {
        var vm = _builder.SurfaceScenario("mapping-ambiguous");

        var exception = vm.FlatExceptions().Single(e => e.Category == ExceptionCategory.OfferSkuAmbiguousMapping);
        Assert.Contains(exception.Evidence, e => e.Source == EvidenceSource.ProductMapping && e.Field == "Candidate");
    }

    [Fact]
    public void Price_mismatch_includes_intended_rrp_and_stripe_amount()
    {
        var vm = _builder.SurfaceScenario("price-mismatch", new ReconciliationOptions(PriceTolerance: Money.Gbp(0)));

        var exception = vm.FlatExceptions().Single(e => e.Category == ExceptionCategory.StripePriceRrpMismatch);
        Assert.Contains(exception.Evidence, e => e.Source == EvidenceSource.IntendedRetailPrice);
        Assert.Contains(exception.Evidence, e => e.Source == EvidenceSource.StripeSubscriptionItem);
    }
}
