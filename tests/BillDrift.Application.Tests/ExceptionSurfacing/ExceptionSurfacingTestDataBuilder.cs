using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

/// <summary>
/// Programmatic builder for exception surfacing integration scenarios.
/// </summary>
public static class ExceptionSurfacingTestDataBuilder
{
    private static readonly SourceReference TestSource = SourceReference.FromRawImportId(
        RawImportId.Create(ImportSourceKind.StripeExport, "exception-fixture", "line-1"));

    private static readonly OfferId DefaultOffer = OfferId.Create("OFFER-MS365-BB");
    private static readonly SkuId DefaultSku = SkuId.Create("SKU-MS365-BB");

    /// <summary>Three customers with Error, Warning, and clean alignment.</summary>
    public static ReconciliationInputs MixedThreeCustomers()
    {
        var errMex = MexId.Create("MEX-ERR-001");
        var warnMex = MexId.Create("MEX-WARN-001");
        var infoMex = MexId.Create("MEX-INFO-001");

        return new ReconciliationInputs(
            [
                CreateSupplierLine(errMex, Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1"), "Microsoft 365 Business Basic", 10),
                CreateNonCspSupplierLine(warnMex, Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc2"))
            ],
            [
                CreateSubscriptionLine(errMex, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), 10),
                CreateSubscriptionLine(infoMex, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"), 3)
            ],
            [
                CreateIntendedPrice(errMex, Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd01"), Money.Gbp(12m)),
                CreateIntendedPrice(infoMex, Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd03"), Money.Gbp(12m))
            ],
            [
                CreateStripeItem(infoMex, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3"), 3, Money.Gbp(12m))
            ],
            [CreateDefaultMapping(), CreateNonCspMapping()]);
    }

    /// <summary>Mapping ambiguous with additional mismatches for suppression testing.</summary>
    public static ReconciliationInputs SuppressionMappingRootCause() =>
        Reconciliation.ReconciliationTestDataBuilder.DuplicateStripeItems();

    /// <summary>Multiple catalogue gaps for the same commercial key.</summary>
    public static ReconciliationInputs CatalogueConsolidation() =>
        Reconciliation.ReconciliationTestDataBuilder.CatalogueMissing();

    /// <summary>Stripe item with no matching subscription truth.</summary>
    public static ReconciliationInputs OrphanedStripeItem()
    {
        var mex = MexId.Create("MEX-ORPHAN-001");
        return new ReconciliationInputs(
            [],
            [],
            [],
            [CreateStripeItem(mex, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"), 2, Money.Gbp(12m), itemId: "si_orphan_001")],
            [CreateDefaultMapping()]);
    }

    /// <summary>Mex ID conflict between subscription truth and Stripe on same group.</summary>
    public static ReconciliationInputs MexIdMismatch()
    {
        var truthMex = MexId.Create("MEX-TRUTH-001");
        var stripeMex = MexId.Create("MEX-STRIPE-002");
        var offer = DefaultOffer;
        var sku = DefaultSku;
        var sharedStripeCustomer = StripeCustomerId.Create("cus_shared_mismatch");

        return new ReconciliationInputs(
            [],
            [
                new MicrosoftSubscriptionLine(
                    MicrosoftSubscriptionLineId.FromGuid(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                    CustomerIdentity.Create(truthMex, "Truth Customer", null, sharedStripeCustomer),
                    CommercialKeyRoot.Create(offer, sku),
                    10,
                    Term.P1M,
                    BillingFrequency.Monthly,
                    new DateOnly(2026, 2, 1),
                    SubscriptionStatus.Active,
                    null,
                    TestSource)
            ],
            [CreateIntendedPrice(truthMex, Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), Money.Gbp(12m))],
            [
                new StripeBillingItem(
                    StripeBillingItemId.FromGuid(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
                    CustomerIdentity.Create(stripeMex, "Stripe Customer", null, sharedStripeCustomer),
                    StripeSubscriptionId.Create("sub_mismatch"),
                    StripeSubscriptionItemId.Create("si_mismatch"),
                    StripeProductId.Create("prod_test001"),
                    StripePriceId.Create("price_test001"),
                    10,
                    BillingFrequency.Monthly,
                    Money.Gbp(12m),
                    new StripeMappingMetadata(stripeMex, offer, sku, [], new Dictionary<string, string>()),
                    TestSource)
            ],
            [CreateDefaultMapping(), CreateNonCspMapping()]);
    }

    /// <summary>Bill-impacting mismatch for low-confidence guard testing.</summary>
    public static ReconciliationInputs LowConfidenceNoAction() =>
        Reconciliation.ReconciliationTestDataBuilder.QuantityMismatch();

    private static MicrosoftSubscriptionLine CreateSubscriptionLine(MexId mex, Guid id, int licenceCount) =>
        new(
            MicrosoftSubscriptionLineId.FromGuid(id),
            CustomerIdentity.Create(mex, $"Customer {mex.Value}", null, StripeCustomerId.Create($"cus_{mex.Value.ToLowerInvariant()}")),
            CommercialKeyRoot.Create(DefaultOffer, DefaultSku),
            licenceCount,
            Term.P1M,
            BillingFrequency.Monthly,
            new DateOnly(2026, 2, 1),
            SubscriptionStatus.Active,
            null,
            TestSource);

    private static StripeBillingItem CreateStripeItem(
        MexId mex,
        Guid id,
        long quantity,
        Money unitAmount,
        BillingFrequency frequency = BillingFrequency.Monthly,
        string itemId = "si_test001") =>
        new(
            StripeBillingItemId.FromGuid(id),
            CustomerIdentity.Create(mex, $"Customer {mex.Value}", null, StripeCustomerId.Create($"cus_{mex.Value.ToLowerInvariant()}")),
            StripeSubscriptionId.Create("sub_test001"),
            StripeSubscriptionItemId.Create(itemId),
            StripeProductId.Create("prod_test001"),
            StripePriceId.Create("price_test001"),
            quantity,
            frequency,
            unitAmount,
            new StripeMappingMetadata(mex, DefaultOffer, DefaultSku, [], new Dictionary<string, string>()),
            TestSource);

    private static SupplierCostLine CreateSupplierLine(MexId mex, Guid id, string productName, int quantity) =>
        new(
            SupplierCostLineId.FromGuid(id),
            CustomerIdentity.Create(mex),
            productName,
            quantity,
            ChargeType.Recurring,
            Reconciliation.ReconciliationTestDataBuilder.DefaultScope,
            Money.Gbp(quantity * 10m),
            [],
            TestSource);

    private static IntendedPrice CreateIntendedPrice(MexId mex, Guid id, Money rrp) =>
        new(
            IntendedPriceId.FromGuid(id),
            CommercialKey.Create(DefaultOffer, DefaultSku, Term.P1M, BillingFrequency.Monthly),
            Money.Gbp(rrp.Amount * 0.8m),
            rrp,
            null,
            null,
            PriceListStatus.Active,
            PriceSource.Catalogue,
            TestSource);

    private static SupplierCostLine CreateNonCspSupplierLine(MexId mex, Guid id) =>
        new(
            SupplierCostLineId.FromGuid(id),
            CustomerIdentity.Create(mex),
            "Non-CSP Software",
            1,
            ChargeType.Recurring,
            Reconciliation.ReconciliationTestDataBuilder.DefaultScope,
            Money.Gbp(10m),
            [],
            TestSource);

    private static ProductMapping CreateNonCspMapping() =>
        new(
            ProductMappingId.FromGuid(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")),
            CommercialKeyRoot.Create(OfferId.Create("OFFER-NONCSP"), SkuId.Create("SKU-NONCSP")),
            "Non-CSP Software",
            StripeProductId.Create("prod_noncsp"),
            new Dictionary<PriceTermKey, StripePriceId>(),
            [new SupplierNameVariant("non-csp software", "Non-CSP Software")],
            ProductClassification.NonCsp,
            MappingConfidence.High,
            MappingSource.Manual);

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
                new SupplierNameVariant("microsoft 365 business basic", "Microsoft 365 Business Basic")
            ],
            ProductClassification.Csp,
            MappingConfidence.High,
            MappingSource.Manual);
}
