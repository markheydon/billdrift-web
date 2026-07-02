using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;

namespace BillDrift.Application.Reconciliation.Indexing;

/// <summary>
/// Index of Stripe products and prices extracted from billing items for catalogue and matching lookups.
/// </summary>
public sealed class StripeCatalogueIndex
{
    private readonly Dictionary<StripePriceId, StripePriceSnapshot> _prices = new();
    private readonly Dictionary<StripeProductId, StripeProductSnapshot> _products = new();
    private readonly List<StripeBillingItem> _items = [];

    /// <summary>
    /// Builds a catalogue index from normalized Stripe billing items.
    /// </summary>
    /// <param name="items">Stripe billing items from the reconciliation input snapshot.</param>
    /// <returns>A populated Stripe catalogue index.</returns>
    public static StripeCatalogueIndex Build(IReadOnlyList<StripeBillingItem> items)
    {
        var index = new StripeCatalogueIndex();
        foreach (var item in items)
        {
            index._items.Add(item);

            if (!index._products.ContainsKey(item.ProductId))
            {
                index._products[item.ProductId] = new StripeProductSnapshot(
                    item.ProductId,
                    item.ProductId.Value,
                    item.MappingMetadata.OfferId,
                    item.MappingMetadata.SkuId);
            }

            index._prices[item.PriceId] = new StripePriceSnapshot(
                item.PriceId,
                item.ProductId,
                item.UnitAmount,
                item.Frequency,
                item.UnitAmount.Currency);
        }

        return index;
    }

    /// <summary>
    /// Attempts to retrieve a price snapshot by Stripe price ID.
    /// </summary>
    public bool TryGetPrice(StripePriceId id, out StripePriceSnapshot price) =>
        _prices.TryGetValue(id, out price!);

    /// <summary>
    /// Attempts to retrieve a product snapshot by Stripe product ID.
    /// </summary>
    public bool TryGetProduct(StripeProductId id, out StripeProductSnapshot product) =>
        _products.TryGetValue(id, out product!);

    /// <summary>
    /// Finds all price snapshots whose product metadata matches the given commercial key root.
    /// </summary>
    public IReadOnlyList<StripePriceSnapshot> FindPrices(CommercialKeyRoot root) =>
        _prices.Values
            .Where(p => _products.TryGetValue(p.ProductId, out var prod) &&
                        prod.OfferId == root.OfferId &&
                        prod.SkuId == root.SkuId)
            .OrderBy(p => p.PriceId.Value, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Finds Stripe billing items for a customer and commercial key.
    /// </summary>
    public IReadOnlyList<StripeBillingItem> FindItems(CustomerIdentity customer, CommercialKey key) =>
        _items
            .Where(i => i.Customer.MexId == customer.MexId &&
                        i.MappingMetadata.OfferId == key.OfferId &&
                        i.MappingMetadata.SkuId == key.SkuId &&
                        FrequenciesCompatible(i.Frequency, key.Frequency))
            .OrderBy(i => i.SubscriptionItemId.Value, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Finds Stripe billing items for a customer and commercial key root, ignoring frequency (for mismatch detection).
    /// </summary>
    public IReadOnlyList<StripeBillingItem> FindItemsByRootIgnoringFrequency(CustomerIdentity customer, CommercialKeyRoot root) =>
        _items
            .Where(i => i.Customer.MexId == customer.MexId &&
                        i.MappingMetadata.OfferId == root.OfferId &&
                        i.MappingMetadata.SkuId == root.SkuId)
            .OrderBy(i => i.SubscriptionItemId.Value, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Finds Stripe billing items for a customer and commercial key root (any frequency).
    /// </summary>
    public IReadOnlyList<StripeBillingItem> FindItemsByRoot(CustomerIdentity customer, CommercialKeyRoot root) =>
        _items
            .Where(i => i.Customer.MexId == customer.MexId &&
                        i.MappingMetadata.OfferId == root.OfferId &&
                        i.MappingMetadata.SkuId == root.SkuId)
            .OrderBy(i => i.SubscriptionItemId.Value, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Finds a price snapshot matching the commercial key's term and frequency.
    /// </summary>
    public StripePriceSnapshot? FindPriceForKey(CommercialKey key)
    {
        var candidates = FindPrices(CommercialKeyRoot.Create(key.OfferId, key.SkuId));
        return candidates.FirstOrDefault(p => p.Interval == key.Frequency);
    }

    private static bool FrequenciesCompatible(BillingFrequency stripe, BillingFrequency expected) =>
        stripe == expected || stripe == BillingFrequency.Unknown || expected == BillingFrequency.Unknown;
}
