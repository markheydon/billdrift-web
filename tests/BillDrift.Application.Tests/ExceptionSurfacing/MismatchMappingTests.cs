using BillDrift.Application.Reconciliation;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Common;
using FluentAssertions;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

public class MismatchMappingTests
{
    private readonly ExceptionSurfacingTestBuilder _builder = new();

    [Theory]
    [InlineData("missing-in-stripe", ExceptionCategory.MissingBillingItem, ReconciliationDomain.TruthVsStripe)]
    [InlineData("quantity-mismatch", ExceptionCategory.QuantityLicenceMismatch, ReconciliationDomain.TruthVsStripe)]
    [InlineData("billing-frequency-mismatch", ExceptionCategory.BillingFrequencyMismatch, ReconciliationDomain.TruthVsStripe)]
    [InlineData("price-mismatch", ExceptionCategory.StripePriceRrpMismatch, ReconciliationDomain.TruthVsStripe)]
    [InlineData("mapping-ambiguous", ExceptionCategory.OfferSkuAmbiguousMapping, ReconciliationDomain.SupplierCostVsMapping)]
    [InlineData("mapping-missing", ExceptionCategory.OfferSkuAmbiguousMapping, ReconciliationDomain.SupplierCostVsMapping)]
    public void Maps_primary_mismatch_type_to_category_and_domain(
        string scenario,
        ExceptionCategory expectedCategory,
        ReconciliationDomain expectedDomain)
    {
        var vm = _builder.SurfaceScenario(scenario, new ReconciliationOptions(PriceTolerance: Money.Gbp(0)));

        vm.FlatExceptions().Should().Contain(e =>
            e.Category == expectedCategory && e.Domain == expectedDomain);
    }

    [Fact]
    public void Catalogue_missing_subdivides_to_stripe_price_missing()
    {
        var vm = _builder.SurfaceScenario("catalogue-missing");

        vm.FlatExceptions().Should().Contain(e =>
            e.Category == ExceptionCategory.StripePriceMissing ||
            e.Category == ExceptionCategory.StripeProductMissing);
    }

    [Fact]
    public void Non_csp_maps_to_non_csp_manual_review()
    {
        var vm = _builder.SurfaceScenario("non-csp-supplier-line");

        vm.FlatExceptions().Should().Contain(e => e.Category == ExceptionCategory.NonCspManualReview);
    }

    [Fact]
    public void Each_exception_has_non_empty_explanation()
    {
        var scenarios = new[]
        {
            "missing-in-stripe", "quantity-mismatch", "billing-frequency-mismatch",
            "price-mismatch", "catalogue-missing", "mapping-missing", "mapping-ambiguous"
        };

        foreach (var scenario in scenarios)
        {
            var vm = _builder.SurfaceScenario(scenario, new ReconciliationOptions(PriceTolerance: Money.Gbp(0)));
            vm.FlatExceptions().Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.Explanation),
                because: $"scenario {scenario} should produce explained exceptions");
        }
    }
}
