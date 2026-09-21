using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Tests.Import;

namespace BillDrift.Domain.Tests.Billing;

public class NormalizedEntityTests
{
    [Fact]
    public void SupplierCostLine_links_to_source_reference()
    {
        var raw = FixtureLoader.LoadGiacomBillingSample();
        var customer = CustomerIdentity.Create(MexId.Create(raw.MexIdRaw.Trim()));
        var source = SourceReference.FromRawImportId(raw.Id);

        var line = new SupplierCostLine(
            SupplierCostLineId.New(),
            customer,
            raw.ProductNameRaw.Trim(),
            10,
            ChargeType.Recurring,
            BillingPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)),
            Money.Gbp(120m),
            [],
            source);

        Assert.Equal("REF-1001", line.Source.SourceLineKey);
        Assert.Equal("Microsoft 365 Business Basic", line.ProductName);
    }

    [Fact]
    public void StripeBillingItem_carries_mapping_metadata()
    {
        var item = new StripeBillingItem(
            StripeBillingItemId.New(),
            CustomerIdentity.Create(MexId.Create("MEX1")),
            StripeSubscriptionId.Create("sub_test123"),
            StripeSubscriptionItemId.Create("si_test456"),
            StripeProductId.Create("prod_test789"),
            StripePriceId.Create("price_test000"),
            5,
            BillingFrequency.Monthly,
            Money.Gbp(12m),
            new StripeMappingMetadata(
                MexId.Create("MEX1"),
                OfferId.Create("OFFER-1"),
                SkuId.Create("SKU-1"),
                [],
                new Dictionary<string, string>()),
            SourceReference.FromRawImportId(
                RawImportId.Create(ImportSourceKind.StripeExport, "stripe-export", "si_test456")));

        Assert.Equal("OFFER-1", item.MappingMetadata.OfferId!.Value.Value);
    }
}
