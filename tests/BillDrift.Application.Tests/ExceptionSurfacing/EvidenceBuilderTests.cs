using BillDrift.Application.Reconciliation;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Common;
using FluentAssertions;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

public class EvidenceBuilderTests
{
    private readonly ExceptionSurfacingTestBuilder _builder = new();

    [Fact]
    public void Quantity_mismatch_includes_subscription_truth_and_stripe_sources()
    {
        var vm = _builder.SurfaceScenario("quantity-mismatch", new ReconciliationOptions(PriceTolerance: Money.Gbp(0)));

        var exception = vm.FlatExceptions().Single(e => e.Category == ExceptionCategory.QuantityLicenceMismatch);
        exception.Evidence.Select(e => e.Source).Should().Contain(EvidenceSource.SubscriptionTruth);
        exception.Evidence.Select(e => e.Source).Should().Contain(EvidenceSource.StripeSubscriptionItem);
        exception.Explanation.Should().Contain("10");
        exception.Explanation.Should().Contain("5");
    }

    [Fact]
    public void Mapping_ambiguous_lists_candidate_evidence()
    {
        var vm = _builder.SurfaceScenario("mapping-ambiguous");

        var exception = vm.FlatExceptions().Single(e => e.Category == ExceptionCategory.OfferSkuAmbiguousMapping);
        exception.Evidence.Should().Contain(e => e.Source == EvidenceSource.ProductMapping && e.Field == "Candidate");
    }

    [Fact]
    public void Price_mismatch_includes_intended_rrp_and_stripe_amount()
    {
        var vm = _builder.SurfaceScenario("price-mismatch", new ReconciliationOptions(PriceTolerance: Money.Gbp(0)));

        var exception = vm.FlatExceptions().Single(e => e.Category == ExceptionCategory.StripePriceRrpMismatch);
        exception.Evidence.Should().Contain(e => e.Source == EvidenceSource.IntendedRetailPrice);
        exception.Evidence.Should().Contain(e => e.Source == EvidenceSource.StripeSubscriptionItem);
    }
}
