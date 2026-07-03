using BillDrift.Domain.Billing;
using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;

namespace BillDrift.Application.CatalogueReconciliation.Detection;

/// <summary>Creates <see cref="CatalogueException"/> records per catalogue-check-rules contract.</summary>
public sealed class CatalogueExceptionFactory
{
    /// <summary>Creates a missing-product exception (CAT-001).</summary>
    public CatalogueException MissingProduct(ProductMapping mapping) =>
        Create(
            CatalogueExceptionType.MissingProduct,
            MismatchSeverity.Warning,
            null,
            mapping.Key,
            mapping.Id,
            "CAT-001",
            $"Missing Stripe product for offer {mapping.Key.OfferId.Value} / SKU {mapping.Key.SkuId.Value} ({mapping.NormalizedProductName}).",
            $"Stripe product for {mapping.Key.OfferId.Value}/{mapping.Key.SkuId.Value}",
            "Not found in catalogue");

    /// <summary>Creates a missing-price exception (CAT-002).</summary>
    public CatalogueException MissingPrice(
        ProductMapping mapping,
        CommercialKey key,
        IntendedPrice intended) =>
        Create(
            CatalogueExceptionType.MissingPrice,
            MismatchSeverity.Warning,
            key,
            mapping.Key,
            mapping.Id,
            "CAT-002",
            $"Missing Stripe price for {key.OfferId.Value}/{key.SkuId.Value} — {key.Term} / {key.Frequency} (expected RRP {intended.Rrp.Amount} {intended.Rrp.Currency.Value}).",
            $"{intended.Rrp.Amount} {intended.Rrp.Currency.Value}",
            "Not found in catalogue");

    /// <summary>Creates an incorrect-price exception (CAT-003).</summary>
    public CatalogueException IncorrectPrice(
        ProductMapping mapping,
        CommercialKey key,
        IntendedPrice intended,
        StripeCataloguePrice actual) =>
        Create(
            CatalogueExceptionType.IncorrectPrice,
            MismatchSeverity.Warning,
            key,
            mapping.Key,
            mapping.Id,
            "CAT-003",
            $"Stripe price {actual.PriceId.Value} amount {actual.UnitAmount.Amount} {actual.UnitAmount.Currency.Value} does not match intended RRP {intended.Rrp.Amount} {intended.Rrp.Currency.Value} for {key.OfferId.Value}/{key.SkuId.Value}.",
            $"{intended.Rrp.Amount} {intended.Rrp.Currency.Value}",
            $"{actual.UnitAmount.Amount} {actual.UnitAmount.Currency.Value}",
            affectedPriceIds: [actual.PriceId]);

    /// <summary>Creates a duplicate-product exception (CAT-004).</summary>
    public CatalogueException DuplicateProduct(CommercialKeyRoot root, IReadOnlyList<StripeCatalogueProduct> products) =>
        Create(
            CatalogueExceptionType.DuplicateProduct,
            MismatchSeverity.Error,
            null,
            root,
            null,
            "CAT-004",
            $"Duplicate Stripe products for offer {root.OfferId.Value} / SKU {root.SkuId.Value}: {string.Join(", ", products.Select(p => p.ProductId.Value))}.",
            null,
            null,
            affectedProductIds: products.Select(p => p.ProductId).ToList());

    /// <summary>Creates a duplicate-price exception (CAT-005).</summary>
    public CatalogueException DuplicatePrice(
        StripeProductId productId,
        BillingFrequency frequency,
        IReadOnlyList<StripeCataloguePrice> prices) =>
        Create(
            CatalogueExceptionType.DuplicatePrice,
            MismatchSeverity.Error,
            null,
            null,
            null,
            "CAT-005",
            $"Duplicate active Stripe prices for product {productId.Value} interval {frequency}: {string.Join(", ", prices.Select(p => p.PriceId.Value))}.",
            null,
            null,
            affectedPriceIds: prices.Select(p => p.PriceId).ToList());

    /// <summary>Creates a pricing-reference gap exception (CAT-006).</summary>
    public CatalogueException PricingReferenceGap(ProductMapping mapping) =>
        Create(
            CatalogueExceptionType.PricingReferenceGap,
            MismatchSeverity.Info,
            null,
            mapping.Key,
            mapping.Id,
            "CAT-006",
            $"No intended pricing reference for mapped product {mapping.Key.OfferId.Value}/{mapping.Key.SkuId.Value}.",
            null,
            null);

    /// <summary>Creates a mapping-ambiguous exception (CAT-007).</summary>
    public CatalogueException MappingAmbiguous(ProductMapping mapping) =>
        Create(
            CatalogueExceptionType.MappingAmbiguous,
            MismatchSeverity.Error,
            null,
            mapping.Key,
            mapping.Id,
            "CAT-007",
            $"Mapping confidence too low or conflicting for {mapping.Key.OfferId.Value}/{mapping.Key.SkuId.Value}.",
            null,
            null);

    /// <summary>Creates an unmapped catalogue entry exception (CAT-008).</summary>
    public CatalogueException UnmappedProduct(StripeCatalogueProduct product) =>
        Create(
            CatalogueExceptionType.UnmappedCatalogueEntry,
            MismatchSeverity.Info,
            null,
            null,
            null,
            "CAT-008",
            $"Stripe product {product.ProductId.Value} has no offer/SKU metadata or canonical mapping.",
            null,
            null,
            affectedProductIds: [product.ProductId]);

    private static CatalogueException Create(
        CatalogueExceptionType type,
        MismatchSeverity severity,
        CommercialKey? key,
        CommercialKeyRoot? root,
        ProductMappingId? mappingId,
        string ruleId,
        string description,
        string? expected,
        string? actual,
        IReadOnlyList<StripeProductId>? affectedProductIds = null,
        IReadOnlyList<StripePriceId>? affectedPriceIds = null) =>
        new(
            CatalogueExceptionId.New(),
            type,
            key,
            root,
            severity,
            description,
            expected,
            actual,
            affectedProductIds ?? [],
            affectedPriceIds ?? [],
            mappingId,
            ruleId);
}
