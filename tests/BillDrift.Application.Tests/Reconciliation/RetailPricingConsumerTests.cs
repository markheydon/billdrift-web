using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using FluentAssertions;

namespace BillDrift.Application.Tests.Reconciliation;

/// <summary>
/// Smoke tests wiring retail pricing ingestion output into the reconciliation engine.
/// </summary>
public sealed class RetailPricingConsumerTests
{
    [Fact]
    public void Ingested_resolved_prices_surface_price_mismatch_against_stripe()
    {
        var key = CommercialKey.Create(
            OfferId.Create("OFFER-MS365-BB"),
            SkuId.Create("SKU-MS365-BB"),
            Term.P1M,
            BillingFrequency.Monthly);

        var source = SourceReference.FromRawImportId(
            RawImportId.Create(ImportSourceKind.GiacomPriceList, "sample-doc", "1"));

        var resolvedFromIngestion = new IntendedPrice(
            IntendedPriceId.New(),
            key,
            Money.Gbp(8.50m),
            Money.Gbp(12.00m),
            Money.Gbp(3.50m),
            29.17m,
            PriceListStatus.Active,
            PriceSource.Catalogue,
            source,
            PricingPlatform.Nce,
            ProductClassification.Csp);

        var inputs = ReconciliationInputsFixtureLoader.Load("price-mismatch") with
        {
            IntendedPrices = [resolvedFromIngestion]
        };

        var run = new ReconciliationEngine(new Mapping.ProductMappingResolver()).Execute(
            new ReconciliationRequest(
                null,
                ReconciliationTestDataBuilder.DefaultScope,
                inputs,
                new ReconciliationOptions(PriceTolerance: Money.Gbp(0))));

        run.Mismatches.Should().Contain(m => m.Type == MismatchType.PriceMismatch);
    }
}
