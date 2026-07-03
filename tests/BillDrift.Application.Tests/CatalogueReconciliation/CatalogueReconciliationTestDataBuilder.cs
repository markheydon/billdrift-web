using BillDrift.Domain.Billing;
using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;

namespace BillDrift.Application.Tests.CatalogueReconciliation;

/// <summary>Programmatic builder for catalogue reconciliation test inputs.</summary>
public static class CatalogueReconciliationTestDataBuilder
{
    private static readonly OfferId DefaultOffer = OfferId.Create("OFFER-MS365-BB");
    private static readonly SkuId DefaultSku = SkuId.Create("SKU-MS365-BB");
    private static readonly StripeProductId DefaultProductId = StripeProductId.Create("prod_test222");
    private static readonly StripePriceId DefaultPriceId = StripePriceId.Create("price_test001");

    private static readonly SourceReference TestSource = SourceReference.FromRawImportId(
        RawImportId.Create(ImportSourceKind.StripeExport, "test", "line-1"));

    /// <summary>All catalogue checks pass.</summary>
    public static CatalogueReconciliationInputs CleanMatch() =>
        Create(
            [CreateProduct()],
            [CreatePrice(Money.Gbp(12m))],
            [CreateDefaultMapping()],
            [CreateIntendedPrice(Money.Gbp(12m))]);

    /// <summary>Stripe product missing for mapped offer/SKU.</summary>
    public static CatalogueReconciliationInputs MissingProduct() =>
        Create(
            [new StripeCatalogueProduct(
                StripeProductId.Create("prod_other"),
                "Other Product",
                OfferId.Create("OTHER"),
                SkuId.Create("OTHER"),
                true,
                new Dictionary<string, string>())],
            [],
            [CreateDefaultMapping()],
            [CreateIntendedPrice(Money.Gbp(12m))]);

    /// <summary>Required Stripe price missing.</summary>
    public static CatalogueReconciliationInputs MissingPrice() =>
        Create(
            [CreateProduct()],
            [],
            [CreateDefaultMapping()],
            [CreateIntendedPrice(Money.Gbp(12m))]);

    /// <summary>Stripe price amount differs from intended RRP.</summary>
    public static CatalogueReconciliationInputs IncorrectPrice() =>
        Create(
            [CreateProduct()],
            [CreatePrice(Money.Gbp(15m))],
            [CreateDefaultMapping()],
            [CreateIntendedPrice(Money.Gbp(12m))]);

    /// <summary>Two Stripe products for same offer/SKU.</summary>
    public static CatalogueReconciliationInputs DuplicateProducts()
    {
        var product2 = CreateProduct() with { ProductId = StripeProductId.Create("prod_dup2") };
        return Create(
            [CreateProduct(), product2],
            [CreatePrice(Money.Gbp(12m), DefaultPriceId), CreatePrice(Money.Gbp(12m), StripePriceId.Create("price_dup2"), product2.ProductId)],
            [CreateDefaultMapping()],
            [CreateIntendedPrice(Money.Gbp(12m))]);
    }

    /// <summary>Two active prices for same interval on one product.</summary>
    public static CatalogueReconciliationInputs DuplicatePrices() =>
        Create(
            [CreateProduct()],
            [
                CreatePrice(Money.Gbp(12m), DefaultPriceId),
                CreatePrice(Money.Gbp(12m), StripePriceId.Create("price_dup2"))
            ],
            [CreateDefaultMapping()],
            [CreateIntendedPrice(Money.Gbp(12m))]);

    /// <summary>Mapped product without intended pricing.</summary>
    public static CatalogueReconciliationInputs PricingReferenceGap() =>
        Create(
            [CreateProduct()],
            [CreatePrice(Money.Gbp(12m))],
            [CreateDefaultMapping()],
            []);

    /// <summary>Stripe product without metadata or mapping.</summary>
    public static CatalogueReconciliationInputs UnmappedStripeProduct() =>
        Create(
            [new StripeCatalogueProduct(
                StripeProductId.Create("prod_orphan"),
                "Orphan Product",
                null,
                null,
                true,
                new Dictionary<string, string>())],
            [],
            [CreateDefaultMapping()],
            [CreateIntendedPrice(Money.Gbp(12m))]);

    /// <summary>Manual override RRP should win for comparison.</summary>
    public static CatalogueReconciliationInputs ManualOverrideRrp()
    {
        var key = CommercialKey.Create(DefaultOffer, DefaultSku, Term.P1M, BillingFrequency.Monthly);
        var catalogue = new IntendedPrice(
            IntendedPriceId.New(),
            key,
            Money.Gbp(8m),
            Money.Gbp(10m),
            null,
            null,
            PriceListStatus.Active,
            PriceSource.Catalogue,
            TestSource);

        var manual = new IntendedPrice(
            IntendedPriceId.New(),
            key,
            Money.Gbp(8m),
            Money.Gbp(12m),
            null,
            null,
            PriceListStatus.Active,
            PriceSource.ManualOverride,
            TestSource);

        return Create(
            [CreateProduct()],
            [CreatePrice(Money.Gbp(12m))],
            [CreateDefaultMapping()],
            [catalogue, manual]);
    }

    private static CatalogueReconciliationInputs Create(
        IReadOnlyList<StripeCatalogueProduct> products,
        IReadOnlyList<StripeCataloguePrice> prices,
        IReadOnlyList<ProductMapping> mappings,
        IReadOnlyList<IntendedPrice> intended) =>
        new(
            products,
            prices,
            mappings,
            intended,
            new CatalogueInputReferences(null, null, null, null));

    private static StripeCatalogueProduct CreateProduct() =>
        new(
            DefaultProductId,
            "Microsoft 365 Business Basic",
            DefaultOffer,
            DefaultSku,
            true,
            new Dictionary<string, string>
            {
                ["offer_id"] = DefaultOffer.Value,
                ["sku_id"] = DefaultSku.Value
            });

    private static StripeCataloguePrice CreatePrice(
        Money amount,
        StripePriceId? priceId = null,
        StripeProductId? productId = null) =>
        new(
            priceId ?? DefaultPriceId,
            productId ?? DefaultProductId,
            amount,
            BillingFrequency.Monthly,
            Term.P1M,
            true);

    private static IntendedPrice CreateIntendedPrice(Money rrp) =>
        new(
            IntendedPriceId.New(),
            CommercialKey.Create(DefaultOffer, DefaultSku, Term.P1M, BillingFrequency.Monthly),
            Money.Gbp(8m),
            rrp,
            null,
            null,
            PriceListStatus.Active,
            PriceSource.Catalogue,
            TestSource);

    private static ProductMapping CreateDefaultMapping() =>
        new(
            ProductMappingId.FromGuid(Guid.Parse("a1111111-1111-1111-1111-111111111111")),
            CommercialKeyRoot.Create(DefaultOffer, DefaultSku),
            "Microsoft 365 Business Basic",
            DefaultProductId,
            new Dictionary<PriceTermKey, StripePriceId>(),
            [],
            ProductClassification.Csp,
            MappingConfidence.High,
            MappingSource.Manual);
}
