using BillDrift.Application.CatalogueReconciliation;
using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Common;
using FluentAssertions;

namespace BillDrift.Application.Tests.CatalogueReconciliation;

public class StripeCatalogueSnapshotIndexTests
{
    [Fact]
    public void FindProducts_returns_metadata_matches_for_commercial_root()
    {
        var offer = OfferId.Create("OFFER-1");
        var sku = SkuId.Create("SKU-1");
        var product = new StripeCatalogueProduct(
            StripeProductId.Create("prod_1"),
            "Test",
            offer,
            sku,
            true,
            new Dictionary<string, string>());

        var index = StripeCatalogueSnapshotIndex.Build([product], []);
        var found = index.FindProducts(CommercialKeyRoot.Create(offer, sku));

        found.Should().ContainSingle(p => p.ProductId.Value == "prod_1");
    }
}
