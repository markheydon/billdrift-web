using BillDrift.Application.Normalization;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;

namespace BillDrift.Domain.Tests.Billing;

public class IntendedPricePrecedenceTests
{
    private readonly IntendedPriceResolver _resolver = new();

    [Fact]
    public void ManualOverride_beats_Catalogue_for_same_commercial_key()
    {
        var key = CommercialKey.Create(
            OfferId.Create("OFFER-1"),
            SkuId.Create("SKU-1"),
            Term.Annual,
            BillingFrequency.Monthly);

        var catalogue = CreatePrice(key, PriceSource.Catalogue, 10m);
        var manual = CreatePrice(key, PriceSource.ManualOverride, 15m);

        var resolved = _resolver.Resolve(key, [catalogue, manual]);

        Assert.NotNull(resolved);
        Assert.Equal(PriceSource.ManualOverride, resolved!.Source);
        Assert.Equal(15m, resolved.Rrp.Amount);
    }

    private static IntendedPrice CreatePrice(CommercialKey key, PriceSource source, decimal rrp) =>
        new(
            IntendedPriceId.New(),
            key,
            Money.Gbp(rrp - 2m),
            Money.Gbp(rrp),
            null,
            null,
            PriceListStatus.Active,
            source,
            SourceReference.FromRawImportId(
                RawImportId.Create(ImportSourceKind.GiacomPriceList, "price-list.csv", "row-1")));
}
