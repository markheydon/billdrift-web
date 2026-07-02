using System.Globalization;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import.Stripe;
using BillDrift.Infrastructure.Import.Stripe.Internal;

namespace BillDrift.Infrastructure.Import.Stripe;

internal static class RawStripeRecordMapper
{
    public static RawStripeSubscriptionItem? MapSubscriptionItem(
        ParsedSubscriptionRow row,
        string sourceDocumentId)
    {
        if (string.IsNullOrWhiteSpace(row.CustomerId) ||
            string.IsNullOrWhiteSpace(row.SubscriptionId) ||
            string.IsNullOrWhiteSpace(row.ProductId) ||
            string.IsNullOrWhiteSpace(row.PriceId))
        {
            return null;
        }

        if (!TryParseQuantity(row.QuantityRaw, out var quantity))
        {
            return null;
        }

        var subscriptionId = row.SubscriptionId.Trim();
        var hasItemId = !string.IsNullOrWhiteSpace(row.SubscriptionItemId);
        var itemId = hasItemId ? row.SubscriptionItemId!.Trim() : subscriptionId;
        var lineKey = hasItemId
            ? row.SubscriptionItemId!.Trim()
            : $"{subscriptionId}:{row.RowNumber}";

        var metadata = StripeMetadataParser.NormalizeMetadataKeys(row.Metadata);
        NormalizeMappingKeys(metadata);

        return new RawStripeSubscriptionItem(
            RawImportId.Create(ImportSourceKind.StripeExport, sourceDocumentId, lineKey),
            itemId,
            subscriptionId,
            row.PriceId.Trim(),
            row.ProductId.Trim(),
            row.CustomerId.Trim(),
            quantity,
            row.ProductName?.Trim(),
            row.Status?.Trim() ?? string.Empty,
            row.UnitAmountRaw?.Trim(),
            row.IntervalRaw?.Trim(),
            row.RowNumber,
            metadata);
    }

    public static RawStripeCustomer? MapCustomer(ParsedSubscriptionRow row)
    {
        if (string.IsNullOrWhiteSpace(row.CustomerId))
        {
            return null;
        }

        var metadata = StripeMetadataParser.NormalizeMetadataKeys(row.Metadata);
        return new RawStripeCustomer(
            row.CustomerId.Trim(),
            row.CustomerName?.Trim(),
            metadata);
    }

    public static RawStripeSubscription? MapSubscription(ParsedSubscriptionRow row)
    {
        if (string.IsNullOrWhiteSpace(row.SubscriptionId) || string.IsNullOrWhiteSpace(row.CustomerId))
        {
            return null;
        }

        var metadata = StripeMetadataParser.NormalizeMetadataKeys(row.Metadata);
        return new RawStripeSubscription(
            row.SubscriptionId.Trim(),
            row.CustomerId.Trim(),
            row.Status?.Trim() ?? string.Empty,
            metadata);
    }

    public static RawStripeProduct? MapProduct(ParsedProductRow row, string sourceDocumentId)
    {
        if (string.IsNullOrWhiteSpace(row.ProductId) || string.IsNullOrWhiteSpace(row.Name))
        {
            return null;
        }

        var metadata = StripeMetadataParser.NormalizeMetadataKeys(row.Metadata);
        foreach (var (key, value) in row.AdditionalFields)
        {
            metadata.TryAdd(key, value);
        }

        return new RawStripeProduct(
            RawImportId.Create(ImportSourceKind.StripeExport, sourceDocumentId, row.ProductId.Trim()),
            row.ProductId.Trim(),
            row.Name.Trim(),
            row.RowNumber,
            metadata);
    }

    public static RawStripePrice? MapPrice(ParsedPriceRow row, string sourceDocumentId)
    {
        if (string.IsNullOrWhiteSpace(row.PriceId) ||
            string.IsNullOrWhiteSpace(row.ProductId) ||
            string.IsNullOrWhiteSpace(row.Currency))
        {
            return null;
        }

        long? unitAmount = null;
        if (!string.IsNullOrWhiteSpace(row.UnitAmountRaw))
        {
            if (!TryParseAmount(row.UnitAmountRaw, row.Currency, out var parsed))
            {
                return null;
            }

            unitAmount = parsed;
        }

        long? intervalCount = null;
        if (!string.IsNullOrWhiteSpace(row.RecurringIntervalCountRaw) &&
            long.TryParse(row.RecurringIntervalCountRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ic))
        {
            intervalCount = ic;
        }

        var metadata = StripeMetadataParser.NormalizeMetadataKeys(row.Metadata);
        foreach (var (key, value) in row.AdditionalFields)
        {
            metadata.TryAdd(key, value);
        }

        return new RawStripePrice(
            RawImportId.Create(ImportSourceKind.StripeExport, sourceDocumentId, row.PriceId.Trim()),
            row.PriceId.Trim(),
            row.ProductId.Trim(),
            unitAmount,
            row.Currency.Trim().ToLowerInvariant(),
            row.RecurringInterval?.Trim(),
            intervalCount,
            row.Description?.Trim(),
            row.RowNumber,
            metadata);
    }

    public static bool TryParseQuantity(string? raw, out long quantity)
    {
        quantity = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) && quantity >= 0;
    }

    public static bool TryParseAmount(string? raw, string currency, out long amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = raw.Trim()
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("£", string.Empty, StringComparison.Ordinal)
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("€", string.Empty, StringComparison.Ordinal);

        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalAmount))
        {
            return false;
        }

        var exponent = GetCurrencyExponent(currency);
        amount = (long)Math.Round(decimalAmount * (decimal)Math.Pow(10, exponent), MidpointRounding.AwayFromZero);
        return true;
    }

    private static void NormalizeMappingKeys(Dictionary<string, string> metadata)
    {
        foreach (var key in metadata.Keys.ToList())
        {
            var normalized = key.Trim().ToLowerInvariant();
            if (!metadata.ContainsKey(normalized))
            {
                metadata[normalized] = metadata[key].Trim();
            }
        }
    }

    private static int GetCurrencyExponent(string currency) =>
        currency.Trim().ToUpperInvariant() switch
        {
            "JPY" => 0,
            _ => 2
        };
}
