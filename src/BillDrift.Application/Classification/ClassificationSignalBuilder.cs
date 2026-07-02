using BillDrift.Domain.Billing;
using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Classification;

/// <summary>
/// Signal bundle computed per item for rule engine evaluation (pipeline Stage 3).
/// </summary>
public sealed record ClassificationSignals(
    ReconciliationItemRef ItemRef,
    MexId CustomerMexId,
    bool HasOfferSku,
    OfferId? OfferId,
    SkuId? SkuId,
    string? NormalizedProductName,
    bool InSubscriptionTruth,
    bool InIntendedPriceList,
    bool HasSupplierCostEvidence,
    bool HasStripeBillingOnly,
    ProductCategory ProductCategory,
    ProductClassification? ProductMappingHint);

/// <summary>
/// Builds per-item signal bundles from reconciliation inputs and cross-item correlation.
/// </summary>
public static class ClassificationSignalBuilder
{
    /// <summary>
    /// Builds signals for a single item reference against the full input snapshot.
    /// </summary>
    public static ClassificationSignals Build(
        ReconciliationItemRef itemRef,
        ReconciliationInputs inputs,
        BillingPeriod scope,
        ClassificationRuleConfiguration config)
    {
        var mexId = itemRef.CustomerMexId;
        var offerSku = ResolveOfferSku(itemRef, inputs);
        var productName = ResolveProductName(itemRef, inputs);
        var category = ResolveProductCategory(offerSku.OfferId, offerSku.SkuId, productName, config);
        var mappingHint = ResolveMappingHint(productName, offerSku.OfferId, offerSku.SkuId, inputs);

        var hasSupplier = HasSupplierEvidence(itemRef, inputs, scope);
        var hasTruth = HasTruthEvidence(itemRef, inputs, mexId, offerSku, productName, inputs.ProductMappings);
        var hasStripe = HasStripeEvidence(itemRef, inputs, mexId);
        var inPriceList = HasIntendedPriceEvidence(offerSku.OfferId, offerSku.SkuId, inputs.IntendedPrices);
        var stripeOnly = HasStripeBillingOnly(itemRef, hasSupplier, hasTruth, hasStripe);

        return new ClassificationSignals(
            itemRef,
            mexId,
            offerSku.HasOfferSku,
            offerSku.OfferId,
            offerSku.SkuId,
            productName,
            hasTruth,
            inPriceList,
            hasSupplier,
            stripeOnly,
            category,
            mappingHint);
    }

    private static (bool HasOfferSku, OfferId? OfferId, SkuId? SkuId) ResolveOfferSku(
        ReconciliationItemRef itemRef,
        ReconciliationInputs inputs)
    {
        return itemRef.Kind switch
        {
            ReconciliationItemKind.SubscriptionTruth => ResolveFromSubscription(itemRef, inputs),
            ReconciliationItemKind.StripeBilling => ResolveFromStripe(itemRef, inputs),
            ReconciliationItemKind.SupplierCost => (false, null, null),
            _ => (false, null, null)
        };
    }

    private static (bool HasOfferSku, OfferId? OfferId, SkuId? SkuId) ResolveFromSubscription(
        ReconciliationItemRef itemRef,
        ReconciliationInputs inputs)
    {
        var line = FindSubscriptionLine(itemRef, inputs);
        if (line is null)
        {
            return (false, null, null);
        }

        return (true, line.CommercialKeyRoot.OfferId, line.CommercialKeyRoot.SkuId);
    }

    private static (bool HasOfferSku, OfferId? OfferId, SkuId? SkuId) ResolveFromStripe(
        ReconciliationItemRef itemRef,
        ReconciliationInputs inputs)
    {
        var item = FindStripeItem(itemRef, inputs);
        if (item is null)
        {
            return (false, null, null);
        }

        var metadata = item.MappingMetadata;
        if (metadata.OfferId is null || metadata.SkuId is null)
        {
            return (false, null, null);
        }

        return (true, metadata.OfferId, metadata.SkuId);
    }

    private static string? ResolveProductName(ReconciliationItemRef itemRef, ReconciliationInputs inputs)
    {
        return itemRef.Kind switch
        {
            ReconciliationItemKind.SupplierCost => FindSupplierLine(itemRef, inputs)?.ProductName,
            ReconciliationItemKind.SubscriptionTruth => ResolveSubscriptionProductName(itemRef, inputs),
            _ => null
        };
    }

    private static string? ResolveSubscriptionProductName(ReconciliationItemRef itemRef, ReconciliationInputs inputs)
    {
        var line = FindSubscriptionLine(itemRef, inputs);
        if (line is null)
        {
            return null;
        }

        var mapping = inputs.ProductMappings.FirstOrDefault(m => m.Key == line.CommercialKeyRoot);
        return mapping?.NormalizedProductName;
    }

    private static ProductCategory ResolveProductCategory(
        OfferId? offerId,
        SkuId? skuId,
        string? productName,
        ClassificationRuleConfiguration config)
    {
        foreach (var rule in config.ProductCategoryRules)
        {
            if (MatchesCategoryRule(rule, offerId, skuId, productName))
            {
                return rule.Category;
            }
        }

        return ProductCategory.Other;
    }

    private static bool MatchesCategoryRule(
        ProductCategoryRule rule,
        OfferId? offerId,
        SkuId? skuId,
        string? productName) =>
        rule.MatchKind switch
        {
            ProductCategoryMatchKind.OfferIdPrefix =>
                offerId is not null &&
                offerId.Value.Value.StartsWith(rule.MatchPattern, StringComparison.OrdinalIgnoreCase),
            ProductCategoryMatchKind.SkuIdPrefix =>
                skuId is not null &&
                skuId.Value.Value.StartsWith(rule.MatchPattern, StringComparison.OrdinalIgnoreCase),
            ProductCategoryMatchKind.ProductNameContains =>
                productName is not null &&
                productName.Contains(rule.MatchPattern, StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    private static ProductClassification? ResolveMappingHint(
        string? productName,
        OfferId? offerId,
        SkuId? skuId,
        ReconciliationInputs inputs)
    {
        if (offerId is not null && skuId is not null)
        {
            var root = CommercialKeyRoot.Create(offerId.Value, skuId.Value);
            var mapping = inputs.ProductMappings.FirstOrDefault(m => m.Key == root);
            if (mapping is not null)
            {
                return mapping.Classification;
            }
        }

        if (!string.IsNullOrWhiteSpace(productName))
        {
            var mapping = inputs.ProductMappings.FirstOrDefault(m =>
                m.SupplierNameVariants.Any(v =>
                    string.Equals(v.NormalizedName, productName, StringComparison.OrdinalIgnoreCase)));
            return mapping?.Classification;
        }

        return null;
    }

    private static bool HasSupplierEvidence(
        ReconciliationItemRef itemRef,
        ReconciliationInputs inputs,
        BillingPeriod scope)
    {
        if (itemRef.Kind == ReconciliationItemKind.SupplierCost)
        {
            return true;
        }

        return inputs.SupplierCostLines.Any(line =>
            line.Customer.MexId == itemRef.CustomerMexId &&
            IsInScope(line.Period, scope));
    }

    private static bool HasTruthEvidence(
        ReconciliationItemRef itemRef,
        ReconciliationInputs inputs,
        MexId mexId,
        (bool HasOfferSku, OfferId? OfferId, SkuId? SkuId) offerSku,
        string? productName,
        IReadOnlyList<ProductMapping> mappings)
    {
        if (itemRef.Kind == ReconciliationItemKind.SubscriptionTruth)
        {
            return true;
        }

        if (offerSku.HasOfferSku && offerSku.OfferId is not null && offerSku.SkuId is not null)
        {
            var root = CommercialKeyRoot.Create(offerSku.OfferId.Value, offerSku.SkuId.Value);
            return inputs.SubscriptionLines.Any(line =>
                line.Customer.MexId == mexId &&
                line.CommercialKeyRoot == root);
        }

        if (!string.IsNullOrWhiteSpace(productName))
        {
            var mapping = mappings.FirstOrDefault(m =>
                m.SupplierNameVariants.Any(v =>
                    string.Equals(v.NormalizedName, productName, StringComparison.OrdinalIgnoreCase)));

            if (mapping is not null)
            {
                return inputs.SubscriptionLines.Any(line =>
                    line.Customer.MexId == mexId &&
                    line.CommercialKeyRoot == mapping.Key);
            }
        }

        return false;
    }

    private static bool HasStripeEvidence(
        ReconciliationItemRef itemRef,
        ReconciliationInputs inputs,
        MexId mexId)
    {
        if (itemRef.Kind == ReconciliationItemKind.StripeBilling)
        {
            return true;
        }

        return inputs.StripeItems.Any(item => item.Customer.MexId == mexId);
    }

    private static bool HasIntendedPriceEvidence(OfferId? offerId, SkuId? skuId, IReadOnlyList<IntendedPrice> prices)
    {
        if (offerId is null || skuId is null)
        {
            return false;
        }

        var root = CommercialKeyRoot.Create(offerId.Value, skuId.Value);
        return prices.Any(price =>
            price.Key.OfferId == root.OfferId &&
            price.Key.SkuId == root.SkuId);
    }

    private static bool HasStripeBillingOnly(
        ReconciliationItemRef itemRef,
        bool hasSupplier,
        bool hasTruth,
        bool hasStripe) =>
        itemRef.Kind == ReconciliationItemKind.StripeBilling && hasStripe && !hasSupplier && !hasTruth;

    private static bool IsInScope(BillingPeriod linePeriod, BillingPeriod scope) =>
        linePeriod.Start <= scope.End && linePeriod.End >= scope.Start;

    private static MicrosoftSubscriptionLine? FindSubscriptionLine(
        ReconciliationItemRef itemRef,
        ReconciliationInputs inputs) =>
        inputs.SubscriptionLines.FirstOrDefault(line =>
            itemRef.EntityId is not null && line.Id.Value == itemRef.EntityId);

    private static StripeBillingItem? FindStripeItem(ReconciliationItemRef itemRef, ReconciliationInputs inputs) =>
        inputs.StripeItems.FirstOrDefault(item =>
            itemRef.EntityId is not null && item.Id.Value == itemRef.EntityId);

    private static SupplierCostLine? FindSupplierLine(ReconciliationItemRef itemRef, ReconciliationInputs inputs) =>
        inputs.SupplierCostLines.FirstOrDefault(line =>
            itemRef.EntityId is not null && line.Id.Value == itemRef.EntityId);
}
