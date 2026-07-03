using BillDrift.Application.CatalogueReconciliation.Detection;
using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Common;

namespace BillDrift.Application.CatalogueReconciliation.Stages;

/// <summary>Detects duplicate Stripe products and prices before presence checks.</summary>
public sealed class DetectDuplicateConflictsStage : ICatalogueReconciliationStage
{
    private readonly CatalogueExceptionFactory _factory = new();

    /// <inheritdoc />
    public void Execute(CatalogueReconciliationContext context)
    {
        if (context.ValidationError is not null || context.CatalogueIndex is null)
        {
            return;
        }

        var byRoot = new Dictionary<(OfferId, SkuId), List<StripeCatalogueProduct>>();
        foreach (var product in context.CatalogueIndex.AllProducts.Where(p => p.IsActive && p.OfferId is not null && p.SkuId is not null))
        {
            var offerId = product.OfferId!.Value;
            var skuId = product.SkuId!.Value;
            var key = (offerId, skuId);
            if (!byRoot.TryGetValue(key, out var list))
            {
                list = [];
                byRoot[key] = list;
            }

            list.Add(product);
        }

        foreach (var ((offerId, skuId), products) in byRoot)
        {
            if (products.Count <= 1)
            {
                continue;
            }

            var root = CommercialKeyRoot.Create(offerId, skuId);
            context.DuplicateProductRoots.Add(root);
            context.Exceptions.Add(_factory.DuplicateProduct(root, products));
        }

        var pricesByProductInterval = new Dictionary<(StripeProductId, BillingFrequency, CurrencyCode), List<StripeCataloguePrice>>();
        foreach (var price in context.CatalogueIndex.AllPrices.Where(p => p.IsActive))
        {
            var key = (price.ProductId, price.Frequency, price.UnitAmount.Currency);
            if (!pricesByProductInterval.TryGetValue(key, out var list))
            {
                list = [];
                pricesByProductInterval[key] = list;
            }

            list.Add(price);
        }

        foreach (var ((productId, frequency, _), prices) in pricesByProductInterval)
        {
            if (prices.Count <= 1)
            {
                continue;
            }

            context.Exceptions.Add(_factory.DuplicatePrice(productId, frequency, prices));
        }
    }
}
