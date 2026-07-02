using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Tests.Reconciliation;

/// <summary>
/// Programmatic builder for reconciliation test inputs covering all quickstart scenarios.
/// </summary>
public static class ReconciliationTestDataBuilder
{
    private static readonly SourceReference TestSource = SourceReference.FromRawImportId(
        RawImportId.Create(ImportSourceKind.StripeExport, "test-fixture", "line-1"));

    private static readonly OfferId DefaultOffer = OfferId.Create("OFFER-MS365-BB");
    private static readonly SkuId DefaultSku = SkuId.Create("SKU-MS365-BB");
    private static readonly MexId DefaultMex = MexId.Create("MEX-TEST-001");

    /// <summary>
    /// Standard billing period for test
    /// </summary>
    public static BillingPeriod DefaultScope =>
        BillingPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

    /// <summary>
    /// Creates inputs where all four domains align (clean match).
    /// </summary>
    public static ReconciliationInputs CleanMatchAllDomains() =>
        new(
            [CreateSupplierLine("Microsoft 365 Business Basic", 10)],
            [CreateSubscriptionLine(10)],
            [CreateIntendedPrice(Money.Gbp(12m))],
            [CreateStripeItem(10, Money.Gbp(12m))],
            [CreateDefaultMapping()]);

    /// <summary>
    /// Creates inputs with active subscription truth but no Stripe item.
    /// </summary>
    public static ReconciliationInputs MissingInStripe() =>
        new(
            [CreateSupplierLine("Microsoft 365 Business Basic", 10)],
            [CreateSubscriptionLine(10)],
            [CreateIntendedPrice(Money.Gbp(12m))],
            [],
            [CreateDefaultMapping()]);

    /// <summary>
    /// Creates inputs with quantity mismatch between truth and Stripe.
    /// </summary>
    public static ReconciliationInputs QuantityMismatch() =>
        new(
            [CreateSupplierLine("Microsoft 365 Business Basic", 10)],
            [CreateSubscriptionLine(10)],
            [CreateIntendedPrice(Money.Gbp(12m))],
            [CreateStripeItem(5, Money.Gbp(12m))],
            [CreateDefaultMapping()]);

    /// <summary>
    /// Creates inputs with billing frequency mismatch.
    /// </summary>
    public static ReconciliationInputs BillingFrequencyMismatch() =>
        new(
            [],
            [CreateSubscriptionLine(10, BillingFrequency.Monthly)],
            [CreateIntendedPrice(Money.Gbp(12m), BillingFrequency.Monthly)],
            [CreateStripeItem(10, Money.Gbp(120m), BillingFrequency.Annual, "price_annual")],
            [CreateDefaultMapping()]);

    /// <summary>
    /// Creates inputs with price mismatch between intended RRP and Stripe.
    /// </summary>
    public static ReconciliationInputs PriceMismatch() =>
        new(
            [],
            [CreateSubscriptionLine(10)],
            [CreateIntendedPrice(Money.Gbp(12m))],
            [CreateStripeItem(10, Money.Gbp(15m))],
            [CreateDefaultMapping()]);

    /// <summary>
    /// Creates inputs where catalogue price is missing for required key.
    /// </summary>
    public static ReconciliationInputs CatalogueMissing() =>
        new(
            [],
            [CreateSubscriptionLine(10)],
            [CreateIntendedPrice(Money.Gbp(12m))],
            [],
            [CreateDefaultMapping()]);

    /// <summary>
    /// Creates inputs with unmapped supplier product name.
    /// </summary>
    public static ReconciliationInputs MappingMissing() =>
        new(
            [CreateSupplierLine("Unknown Product XYZ", 5)],
            [],
            [],
            [],
            [CreateDefaultMapping()]);

    /// <summary>
    /// Creates inputs with duplicate Stripe items for same customer and key.
    /// </summary>
    public static ReconciliationInputs DuplicateStripeItems()
    {
        var item1 = CreateStripeItem(5, Money.Gbp(12m), BillingFrequency.Monthly, "price_a", "si_dup_a");
        var item2 = CreateStripeItem(5, Money.Gbp(12m), BillingFrequency.Monthly, "price_a", "si_dup_b") with
        {
            Id = StripeBillingItemId.FromGuid(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc"))
        };

        return new(
            [],
            [CreateSubscriptionLine(10)],
            [CreateIntendedPrice(Money.Gbp(12m))],
            [item1, item2],
            [CreateDefaultMapping()]);
    }

    /// <summary>
    /// Creates inputs with non-CSP supplier line.
    /// </summary>
    public static ReconciliationInputs NonCspSupplierLine() =>
        new(
            [CreateSupplierLine("Non-CSP Software", 1)],
            [],
            [],
            [],
            [CreateNonCspMapping()]);

    /// <summary>
    /// Creates inputs with subscription truth but no product mapping catalogue entry.
    /// </summary>
    public static ReconciliationInputs SubscriptionTruthMappingMissing() =>
        new(
            [],
            [CreateSubscriptionLine(10)],
            [CreateIntendedPrice(Money.Gbp(12m))],
            [],
            []);

    /// <summary>
    /// Creates inputs with non-CSP mapping on subscription truth only (no supplier line).
    /// </summary>
    public static ReconciliationInputs SubscriptionTruthNonCspOnly() =>
        new(
            [],
            [CreateSubscriptionLine(10)],
            [CreateIntendedPrice(Money.Gbp(12m))],
            [],
            [CreateNonCspMapping()]);

    /// <summary>
    /// Creates inputs with orphan supplier line (no matching truth).
    /// </summary>
    public static ReconciliationInputs SupplierOrphanLine() =>
        new(
            [CreateSupplierLine("Microsoft 365 Business Basic", 3)],
            [],
            [],
            [],
            [CreateDefaultMapping()]);

    /// <summary>
    /// Creates inputs with manual price override beating catalogue for same key.
    /// </summary>
    public static (ReconciliationInputs Inputs, Money OverrideRrp) ManualOverridePrecedence()
    {
        var key = CommercialKey.Create(DefaultOffer, DefaultSku, Term.P1M, BillingFrequency.Monthly);
        var catalogue = new IntendedPrice(
            IntendedPriceId.FromGuid(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            key,
            Money.Gbp(8m),
            Money.Gbp(10m),
            null,
            null,
            PriceListStatus.Active,
            PriceSource.Catalogue,
            TestSource);

        var manual = new IntendedPrice(
            IntendedPriceId.FromGuid(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            key,
            Money.Gbp(8m),
            Money.Gbp(12m),
            null,
            null,
            PriceListStatus.Active,
            PriceSource.ManualOverride,
            TestSource);

        var inputs = new ReconciliationInputs(
            [],
            [CreateSubscriptionLine(10)],
            [catalogue, manual],
            [CreateStripeItem(10, Money.Gbp(15m))],
            [CreateDefaultMapping()]);

        return (inputs, Money.Gbp(12m));
    }

    private static MicrosoftSubscriptionLine CreateSubscriptionLine(
        int licenceCount,
        BillingFrequency frequency = BillingFrequency.Monthly) =>
        new(
            MicrosoftSubscriptionLineId.FromGuid(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            CustomerIdentity.Create(DefaultMex, "Test Customer", null, StripeCustomerId.Create("cus_test001")),
            CommercialKeyRoot.Create(DefaultOffer, DefaultSku),
            licenceCount,
            Term.P1M,
            frequency,
            new DateOnly(2026, 2, 1),
            SubscriptionStatus.Active,
            null,
            TestSource);

    private static StripeBillingItem CreateStripeItem(
        long quantity,
        Money unitAmount,
        BillingFrequency frequency = BillingFrequency.Monthly,
        string priceId = "price_test001",
        string itemId = "si_test001") =>
        new(
            StripeBillingItemId.FromGuid(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            CustomerIdentity.Create(DefaultMex, "Test Customer", null, StripeCustomerId.Create("cus_test001")),
            StripeSubscriptionId.Create("sub_test001"),
            StripeSubscriptionItemId.Create(itemId),
            StripeProductId.Create("prod_test001"),
            StripePriceId.Create(priceId),
            quantity,
            frequency,
            unitAmount,
            new StripeMappingMetadata(DefaultMex, DefaultOffer, DefaultSku, [], new Dictionary<string, string>()),
            TestSource);

    private static SupplierCostLine CreateSupplierLine(string productName, int quantity) =>
        new(
            SupplierCostLineId.FromGuid(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")),
            CustomerIdentity.Create(DefaultMex),
            productName,
            quantity,
            ChargeType.Recurring,
            DefaultScope,
            Money.Gbp(quantity * 10m),
            [],
            TestSource);

    private static IntendedPrice CreateIntendedPrice(
        Money rrp,
        BillingFrequency frequency = BillingFrequency.Monthly) =>
        new(
            IntendedPriceId.FromGuid(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
            CommercialKey.Create(DefaultOffer, DefaultSku, Term.P1M, frequency),
            Money.Gbp(rrp.Amount * 0.8m),
            rrp,
            null,
            null,
            PriceListStatus.Active,
            PriceSource.Catalogue,
            TestSource);

    private static ProductMapping CreateDefaultMapping() =>
        new(
            ProductMappingId.FromGuid(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")),
            CommercialKeyRoot.Create(DefaultOffer, DefaultSku),
            "Microsoft 365 Business Basic",
            StripeProductId.Create("prod_test001"),
            new Dictionary<PriceTermKey, StripePriceId>
            {
                [new PriceTermKey(Term.P1M, BillingFrequency.Monthly)] = StripePriceId.Create("price_test001")
            },
            [
                new SupplierNameVariant("microsoft 365 business basic", "Microsoft 365 Business Basic"),
                new SupplierNameVariant("ms365 business basic", "MS365 Business Basic")
            ],
            ProductClassification.Csp,
            MappingConfidence.High,
            MappingSource.Manual);

    private static ProductMapping CreateNonCspMapping() =>
        CreateDefaultMapping() with
        {
            NormalizedProductName = "Non-CSP Software",
            Classification = ProductClassification.NonCsp,
            SupplierNameVariants =
            [
                new SupplierNameVariant("non-csp software", "Non-CSP Software")
            ]
        };
}
