using BillDrift.Application.Reconciliation.Indexing;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using FluentAssertions;

namespace BillDrift.Application.Tests.Reconciliation;

public class IntendedPriceIndexTests
{
    [Fact]
    public void Manual_override_wins_over_catalogue_for_same_commercial_key()
    {
        var key = CommercialKey.Create(
            OfferId.Create("OFFER-1"),
            SkuId.Create("SKU-1"),
            Term.P1M,
            BillingFrequency.Monthly);

        var source = SourceReference.FromRawImportId(
            RawImportId.Create(ImportSourceKind.GiacomPriceList, "test", "1"));

        var catalogue = new IntendedPrice(
            IntendedPriceId.New(),
            key,
            Money.Gbp(8m),
            Money.Gbp(10m),
            null,
            null,
            PriceListStatus.Active,
            PriceSource.Catalogue,
            source);

        var manual = new IntendedPrice(
            IntendedPriceId.New(),
            key,
            Money.Gbp(8m),
            Money.Gbp(12m),
            null,
            null,
            PriceListStatus.Active,
            PriceSource.ManualOverride,
            source);

        var index = IntendedPriceIndex.Build([catalogue, manual]);

        index.TryGet(key, out var price).Should().BeTrue();
        price!.Rrp.Amount.Should().Be(12m);
        price.Source.Should().Be(PriceSource.ManualOverride);
    }
}
