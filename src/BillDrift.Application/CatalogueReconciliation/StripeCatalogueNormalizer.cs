using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import.Stripe;

namespace BillDrift.Application.CatalogueReconciliation;

/// <summary>Maps raw Stripe CSV export records into catalogue reconciliation snapshots.</summary>
public sealed class StripeCatalogueNormalizer : IStripeCatalogueNormalizer
{
    /// <inheritdoc />
    public IReadOnlyList<StripeCatalogueProduct> NormalizeProducts(IReadOnlyList<RawStripeProduct> products)
    {
        ArgumentNullException.ThrowIfNull(products);
        var result = new List<StripeCatalogueProduct>(products.Count);

        foreach (var product in products)
        {
            if (string.IsNullOrWhiteSpace(product.ProductId))
            {
                continue;
            }

            var metadata = product.Metadata ?? new Dictionary<string, string>();
            var offerId = TryParseOfferId(metadata);
            var skuId = TryParseSkuId(metadata);
            var isActive = !IsArchived(metadata);

            result.Add(new StripeCatalogueProduct(
                StripeProductId.Create(product.ProductId),
                product.Name ?? product.ProductId,
                offerId,
                skuId,
                isActive,
                metadata));
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<StripeCataloguePrice> NormalizePrices(IReadOnlyList<RawStripePrice> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);
        var result = new List<StripeCataloguePrice>(prices.Count);

        foreach (var price in prices)
        {
            if (string.IsNullOrWhiteSpace(price.PriceId) ||
                string.IsNullOrWhiteSpace(price.ProductId) ||
                string.IsNullOrWhiteSpace(price.Currency) ||
                price.UnitAmount is null)
            {
                continue;
            }

            var currency = CurrencyCode.Create(price.Currency);
            var majorAmount = ConvertMinorToMajor(price.UnitAmount.Value, currency);
            var money = Money.Create(majorAmount, currency);
            var frequency = ParseFrequency(price.RecurringInterval);
            var metadata = price.Metadata ?? new Dictionary<string, string>();
            var isActive = !IsArchived(metadata);

            result.Add(new StripeCataloguePrice(
                StripePriceId.Create(price.PriceId),
                StripeProductId.Create(price.ProductId),
                money,
                frequency,
                null,
                isActive));
        }

        return result;
    }

    private static OfferId? TryParseOfferId(IReadOnlyDictionary<string, string> metadata)
    {
        var raw = GetMetadataValue(metadata, "offer_id", "OfferId");
        return string.IsNullOrWhiteSpace(raw) ? null : OfferId.Create(raw);
    }

    private static SkuId? TryParseSkuId(IReadOnlyDictionary<string, string> metadata)
    {
        var raw = GetMetadataValue(metadata, "sku_id", "SkuId");
        return string.IsNullOrWhiteSpace(raw) ? null : SkuId.Create(raw);
    }

    private static string? GetMetadataValue(IReadOnlyDictionary<string, string> metadata, params string[] keys)
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

    private static bool IsArchived(IReadOnlyDictionary<string, string> metadata)
    {
        var active = GetMetadataValue(metadata, "active", "Active", "archived", "Archived");
        if (active is null)
        {
            return false;
        }

        if (bool.TryParse(active, out var parsed))
        {
            return !parsed;
        }

        return string.Equals(active, "archived", StringComparison.OrdinalIgnoreCase);
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

    /// <summary>Stripe stores amounts in minor currency units; domain money uses major units.</summary>
    private static decimal ConvertMinorToMajor(long minorAmount, CurrencyCode currency)
    {
        var exponent = string.Equals(currency.Value, "GBP", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(currency.Value, "USD", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(currency.Value, "EUR", StringComparison.OrdinalIgnoreCase)
            ? 2
            : 2;

        return minorAmount / (decimal)Math.Pow(10, exponent);
    }
}
