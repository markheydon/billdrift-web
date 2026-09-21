using BillDrift.Application.CatalogueReconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import.Stripe;

namespace BillDrift.Application.Tests.CatalogueReconciliation;

public class StripeCatalogueNormalizerTests
{
    [Fact]
    public void NormalizePrices_converts_stripe_minor_units_to_major_money()
    {
        var normalizer = new StripeCatalogueNormalizer();
        var raw = new RawStripePrice(
            RawImportId.Create(ImportSourceKind.StripeExport, "doc", "price_1"),
            "price_1",
            "prod_1",
            1200,
            "gbp",
            "month",
            1,
            null,
            1,
            new Dictionary<string, string>());

        var prices = normalizer.NormalizePrices([raw]);

        Assert.Single(prices);
        Assert.Equal(12m, prices[0].UnitAmount.Amount);
    }
}
