using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Common;

namespace BillDrift.Application.CatalogueReconciliation;

/// <summary>In-memory index of a full Stripe product and price catalogue snapshot.</summary>
public sealed class StripeCatalogueSnapshotIndex
{
    private readonly Dictionary<StripeProductId, StripeCatalogueProduct> _products = new();
    private readonly Dictionary<StripePriceId, StripeCataloguePrice> _prices = new();
    private readonly List<StripeCatalogueProduct> _allProducts = [];
    private readonly List<StripeCataloguePrice> _allPrices = [];

    /// <summary>Builds an index from normalized catalogue snapshots.</summary>
    public static StripeCatalogueSnapshotIndex Build(
        IReadOnlyList<StripeCatalogueProduct> products,
        IReadOnlyList<StripeCataloguePrice> prices)
    {
        var index = new StripeCatalogueSnapshotIndex();
        foreach (var product in products)
        {
            index._products[product.ProductId] = product;
            index._allProducts.Add(product);
        }

        foreach (var price in prices)
        {
            index._prices[price.PriceId] = price;
            index._allPrices.Add(price);
        }

        return index;
    }

    /// <summary>All products in the snapshot.</summary>
    public IReadOnlyList<StripeCatalogueProduct> AllProducts => _allProducts;

    /// <summary>All prices in the snapshot.</summary>
    public IReadOnlyList<StripeCataloguePrice> AllPrices => _allPrices;

    /// <summary>Attempts to retrieve a product by Stripe product ID.</summary>
    public bool TryGetProduct(StripeProductId id, out StripeCatalogueProduct product) =>
        _products.TryGetValue(id, out product!);

    /// <summary>Finds products matching offer/SKU metadata or mapping ID.</summary>
    public IReadOnlyList<StripeCatalogueProduct> FindProducts(CommercialKeyRoot root)
    {
        return _allProducts
            .Where(p => p.IsActive &&
                        p.OfferId == root.OfferId &&
                        p.SkuId == root.SkuId)
            .OrderBy(p => p.ProductId.Value, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Finds products by Stripe product ID list for duplicate reporting.</summary>
    public IReadOnlyList<StripeCatalogueProduct> FindProductsByIds(IEnumerable<StripeProductId> ids) =>
        ids.Select(id => _products.TryGetValue(id, out var p) ? p : null)
            .Where(p => p is not null)
            .Cast<StripeCatalogueProduct>()
            .ToList();

    /// <summary>Finds active prices for a product and billing frequency.</summary>
    public IReadOnlyList<StripeCataloguePrice> FindActivePrices(
        StripeProductId productId,
        BillingFrequency frequency,
        bool includeArchived)
    {
        return _allPrices
            .Where(p => p.ProductId == productId &&
                        (p.IsActive || includeArchived) &&
                        FrequenciesCompatible(p.Frequency, frequency))
            .OrderBy(p => p.PriceId.Value, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Finds an active price for a commercial key on a resolved product.</summary>
    public StripeCataloguePrice? FindPriceForKey(StripeProductId productId, CommercialKey key, bool includeArchived)
    {
        var candidates = FindActivePrices(productId, key.Frequency, includeArchived);
        return candidates.FirstOrDefault();
    }

    private static bool FrequenciesCompatible(BillingFrequency stripe, BillingFrequency expected) =>
        stripe == expected || stripe == BillingFrequency.Unknown || expected == BillingFrequency.Unknown;
}
