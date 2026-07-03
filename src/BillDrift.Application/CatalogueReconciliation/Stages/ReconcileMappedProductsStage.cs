using BillDrift.Application.CatalogueReconciliation.Detection;
using BillDrift.Domain.Billing;
using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;

namespace BillDrift.Application.CatalogueReconciliation.Stages;

/// <summary>Reconciles each canonical mapped product against Stripe catalogue and intended RRP.</summary>
public sealed class ReconcileMappedProductsStage : ICatalogueReconciliationStage
{
    private readonly CatalogueExceptionFactory _factory = new();

    /// <inheritdoc />
    public void Execute(CatalogueReconciliationContext context)
    {
        if (context.ValidationError is not null ||
            context.CatalogueIndex is null ||
            context.ProductMappingIndex is null ||
            context.IntendedPriceIndex is null)
        {
            return;
        }

        foreach (var mapping in context.ProductMappingIndex.AllMappings)
        {
            if (mapping.Classification == ProductClassification.NonCsp && !context.Options.IncludeNonCspProducts)
            {
                continue;
            }

            if (mapping.Confidence == MappingConfidence.Low || mapping.Confidence == MappingConfidence.Unmapped)
            {
                context.Exceptions.Add(_factory.MappingAmbiguous(mapping));
                continue;
            }

            var keysForRoot = context.IntendedPriceIndex.GetAllKeys()
                .Where(k => k.OfferId == mapping.Key.OfferId && k.SkuId == mapping.Key.SkuId)
                .OrderBy(k => k.Term)
                .ThenBy(k => k.Frequency)
                .ToList();

            if (keysForRoot.Count == 0)
            {
                context.Exceptions.Add(_factory.PricingReferenceGap(mapping));
                continue;
            }

            context.MappedProductsChecked++;

            if (context.DuplicateProductRoots.Contains(mapping.Key))
            {
                continue;
            }

            var product = ResolveProduct(context, mapping);
            if (product is null)
            {
                context.Exceptions.Add(_factory.MissingProduct(mapping));
                continue;
            }

            foreach (var key in keysForRoot)
            {
                if (!context.IntendedPriceIndex.TryGet(key, out var intended))
                {
                    continue;
                }

                var prices = context.CatalogueIndex.FindActivePrices(
                    product.ProductId,
                    key.Frequency,
                    context.Options.IncludeArchivedPrices);

                var activePrice = prices.FirstOrDefault(p => p.IsActive);
                if (activePrice is null && context.Options.IncludeArchivedPrices)
                {
                    activePrice = prices.FirstOrDefault();
                }

                if (activePrice is null)
                {
                    context.Exceptions.Add(_factory.MissingPrice(mapping, key, intended));
                    continue;
                }

                if (!AmountsMatch(intended, activePrice, context.Options))
                {
                    context.Exceptions.Add(_factory.IncorrectPrice(mapping, key, intended, activePrice));
                }
            }
        }
    }

    private static StripeCatalogueProduct? ResolveProduct(CatalogueReconciliationContext context, ProductMapping mapping)
    {
        if (context.CatalogueIndex!.TryGetProduct(mapping.StripeProductId, out var byId) && byId.IsActive)
        {
            return byId;
        }

        var matches = context.CatalogueIndex.FindProducts(mapping.Key);
        return matches.FirstOrDefault();
    }

    private static bool AmountsMatch(IntendedPrice intended, StripeCataloguePrice stripe, CatalogueReconciliationOptions options)
    {
        if (!string.Equals(
                intended.Rrp.Currency.Value,
                stripe.UnitAmount.Currency.Value,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (options.ExactAmountMatch)
        {
            return intended.Rrp.Amount == stripe.UnitAmount.Amount;
        }

        return Math.Abs(intended.Rrp.Amount - stripe.UnitAmount.Amount) < 0.01m;
    }
}
