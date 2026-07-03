using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import.Stripe;

namespace BillDrift.Application.Normalization;

/// <summary>
/// Flattens raw Stripe export objects into normalized <see cref="StripeBillingItem"/> records.
/// </summary>
public sealed class StripeBillingNormalizer : IStripeBillingNormalizer
{
    /// <inheritdoc />
    public IReadOnlyList<StripeBillingItem> Normalize(
        RawStripeCustomer customer,
        IReadOnlyList<RawStripeSubscription> subscriptions,
        IReadOnlyList<RawStripeSubscriptionItem> items,
        IReadOnlyList<RawStripeProduct> products,
        IReadOnlyList<RawStripePrice> prices)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(prices);

        var productById = products.ToDictionary(p => p.ProductId, StringComparer.Ordinal);
        var priceById = prices.ToDictionary(p => p.PriceId, StringComparer.Ordinal);
        var subscriptionsById = subscriptions.ToDictionary(s => s.SubscriptionId, StringComparer.Ordinal);
        var customerItems = items.Where(i => i.CustomerId == customer.CustomerId).ToList();
        var result = new List<StripeBillingItem>(customerItems.Count);

        foreach (var item in customerItems)
        {
            if (!subscriptionsById.TryGetValue(item.SubscriptionId, out var subscription))
            {
                continue;
            }

            productById.TryGetValue(item.ProductId, out var product);
            priceById.TryGetValue(item.PriceId, out var price);

            var mergedMetadata = MergeMetadata(customer.Metadata, subscription.Metadata, item.Metadata, product?.Metadata, price?.Metadata);
            var mapping = BuildMappingMetadata(mergedMetadata);
            var frequency = ParseFrequency(price?.RecurringInterval ?? item.IntervalRaw);
            var unitAmount = ResolveUnitAmount(item, price);
            var displayName = string.IsNullOrWhiteSpace(customer.Name) ? customer.CustomerId : customer.Name;
            var stripeCustomerId = StripeCustomerId.Create(customer.CustomerId);
            var mexId = mapping.MexId ?? MexId.Create("UNKNOWN");
            var customerIdentity = CustomerIdentity.Create(mexId, displayName, null, stripeCustomerId);

            result.Add(new StripeBillingItem(
                StripeBillingItemId.New(),
                customerIdentity,
                StripeSubscriptionId.Create(item.SubscriptionId),
                StripeSubscriptionItemId.Create(item.SubscriptionItemId),
                StripeProductId.Create(item.ProductId),
                StripePriceId.Create(item.PriceId),
                item.Quantity,
                frequency,
                unitAmount,
                mapping,
                SourceReference.FromRawImportId(item.Id)));
        }

        return result;
    }

    private static Dictionary<string, string> MergeMetadata(params IReadOnlyDictionary<string, string>?[] sources)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (source is null)
            {
                continue;
            }

            foreach (var (key, value) in source)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    merged[key.Trim().ToLowerInvariant()] = value.Trim();
                }
            }
        }

        return merged;
    }

    private static StripeMappingMetadata BuildMappingMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        MexId? mexId = TryGetValue(metadata, "mex_id", "mexid") is { } mexRaw
            ? MexId.Create(mexRaw.ToUpperInvariant())
            : null;
        OfferId? offerId = TryGetValue(metadata, "offer_id", "offerid") is { } offerRaw
            ? OfferId.Create(offerRaw)
            : null;
        SkuId? skuId = TryGetValue(metadata, "sku_id", "skuid") is { } skuRaw
            ? SkuId.Create(skuRaw)
            : null;

        var supplierRefs = metadata
            .Where(kvp => kvp.Key.StartsWith("supplier_", StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => SupplierReferenceId.Create(v))
            .ToList();

        var additional = metadata
            .Where(kvp => !IsKnownKey(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        return new StripeMappingMetadata(mexId, offerId, skuId, supplierRefs, additional);
    }

    private static bool IsKnownKey(string key)
    {
        var normalized = key.ToLowerInvariant();
        return normalized is "mex_id" or "mexid" or "offer_id" or "offerid" or "sku_id" or "skuid"
            || normalized.StartsWith("supplier_", StringComparison.Ordinal);
    }

    private static string? TryGetValue(IReadOnlyDictionary<string, string> metadata, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static BillingFrequency ParseFrequency(string? interval)
    {
        if (string.IsNullOrWhiteSpace(interval))
        {
            return BillingFrequency.Unknown;
        }

        return interval.Trim().ToLowerInvariant() switch
        {
            "month" or "monthly" => BillingFrequency.Monthly,
            "year" or "annual" or "yearly" => BillingFrequency.Annual,
            _ => BillingFrequency.Unknown
        };
    }

    private static Money ResolveUnitAmount(RawStripeSubscriptionItem item, RawStripePrice? price)
    {
        if (price?.UnitAmount is long minor)
        {
            return Money.Gbp(minor / 100m);
        }

        if (!string.IsNullOrWhiteSpace(item.UnitAmountRaw) &&
            decimal.TryParse(item.UnitAmountRaw.Trim(), out var parsed))
        {
            return Money.Gbp(parsed);
        }

        return Money.Gbp(0m);
    }
}
