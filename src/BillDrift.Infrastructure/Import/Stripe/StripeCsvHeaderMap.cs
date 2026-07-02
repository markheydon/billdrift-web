using BillDrift.Application.Import;

namespace BillDrift.Infrastructure.Import.Stripe;

/// <summary>
/// Logical field names used by Stripe CSV parsers after header alias resolution.
/// </summary>
internal enum StripeLogicalField
{
    CustomerId,
    CustomerName,
    CustomerEmail,
    SubscriptionId,
    SubscriptionItemId,
    ProductId,
    ProductName,
    PriceId,
    Quantity,
    Status,
    UnitAmount,
    Interval,
    Currency,
    Name,
    RecurringInterval,
    RecurringIntervalCount,
    Description
}

/// <summary>
/// Maps Stripe dashboard CSV column headers to logical ingestion fields via alias registry.
/// </summary>
internal static class StripeCsvHeaderMap
{
    private static readonly IReadOnlyDictionary<StripeLogicalField, string[]> SubscriptionsRequired =
        new Dictionary<StripeLogicalField, string[]>
        {
            [StripeLogicalField.CustomerId] = ["Customer ID", "customer_id", "Customer Id", "cus_id"],
            [StripeLogicalField.SubscriptionId] = ["id", "Subscription ID", "subscription_id", "sub_id"],
            [StripeLogicalField.ProductId] = ["Product ID", "product_id", "Product Id"],
            [StripeLogicalField.PriceId] = ["Price ID", "price_id", "Price Id", "Plan ID", "plan_id"],
            [StripeLogicalField.Quantity] = ["Quantity", "quantity", "Seats", "seats"],
            [StripeLogicalField.Status] = ["Status", "status", "Subscription Status"]
        };

    private static readonly IReadOnlyDictionary<StripeLogicalField, string[]> SubscriptionsOptional =
        new Dictionary<StripeLogicalField, string[]>
        {
            [StripeLogicalField.SubscriptionItemId] = ["Subscription Item ID", "subscription_item_id", "item_id", "si_id"],
            [StripeLogicalField.CustomerName] = ["Customer Name", "customer_name", "Customer Description"],
            [StripeLogicalField.ProductName] = ["Product Name", "product_name", "Plan", "plan"],
            [StripeLogicalField.UnitAmount] = ["Amount", "amount", "Unit Amount", "unit_amount", "Plan Amount"],
            [StripeLogicalField.Interval] = ["Interval", "interval", "Billing Interval", "billing_interval"],
            [StripeLogicalField.Currency] = ["Currency", "currency"],
            [StripeLogicalField.CustomerEmail] = ["Customer Email", "customer_email"]
        };

    private static readonly IReadOnlyDictionary<StripeLogicalField, string[]> ProductsRequired =
        new Dictionary<StripeLogicalField, string[]>
        {
            [StripeLogicalField.ProductId] = ["id", "Product ID", "product_id"],
            [StripeLogicalField.Name] = ["Name", "name", "Product Name", "product_name"]
        };

    private static readonly IReadOnlyDictionary<StripeLogicalField, string[]> PricesRequired =
        new Dictionary<StripeLogicalField, string[]>
        {
            [StripeLogicalField.PriceId] = ["id", "Price ID", "price_id"],
            [StripeLogicalField.ProductId] = ["Product ID", "product_id", "Product Id"],
            [StripeLogicalField.Currency] = ["Currency", "currency"]
        };

    private static readonly IReadOnlyDictionary<StripeLogicalField, string[]> PricesOptional =
        new Dictionary<StripeLogicalField, string[]>
        {
            [StripeLogicalField.UnitAmount] = ["Amount", "amount", "Unit Amount", "unit_amount"],
            [StripeLogicalField.RecurringInterval] = ["Interval", "interval", "Recurring Interval", "recurring_interval"],
            [StripeLogicalField.RecurringIntervalCount] = ["Interval Count", "interval_count", "Recurring Interval Count"],
            [StripeLogicalField.Description] = ["Description", "description", "Nickname", "nickname"]
        };

    public static IReadOnlyDictionary<StripeLogicalField, string> BuildFieldToHeaderMap(
        IReadOnlyList<string> headers,
        StripeCsvFileKind fileKind)
    {
        var required = GetRequiredFields(fileKind);
        var optional = GetOptionalFields(fileKind);
        var map = new Dictionary<StripeLogicalField, string>();

        foreach (var (field, aliases) in required)
        {
            var matched = FindMatchingHeader(headers, aliases);
            if (matched is not null)
            {
                map[field] = matched;
            }
        }

        foreach (var (field, aliases) in optional)
        {
            var matched = FindMatchingHeader(headers, aliases);
            if (matched is not null)
            {
                map[field] = matched;
            }
        }

        return map;
    }

    public static IReadOnlyList<StripeLogicalField> GetMissingRequiredFields(
        IReadOnlyList<string> headers,
        StripeCsvFileKind fileKind)
    {
        var missing = new List<StripeLogicalField>();
        foreach (var (field, aliases) in GetRequiredFields(fileKind))
        {
            if (FindMatchingHeader(headers, aliases) is null)
            {
                missing.Add(field);
            }
        }

        return missing;
    }

    public static bool HasSubscriptionItemIdColumn(IReadOnlyList<string> headers) =>
        FindMatchingHeader(headers, SubscriptionsOptional[StripeLogicalField.SubscriptionItemId]) is not null;

    public static string? GetFieldValue(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<StripeLogicalField, string> fieldMap,
        StripeLogicalField field) =>
        fieldMap.TryGetValue(field, out var header) && row.TryGetValue(header, out var value)
            ? value
            : null;

    private static IReadOnlyDictionary<StripeLogicalField, string[]> GetRequiredFields(StripeCsvFileKind fileKind) =>
        fileKind switch
        {
            StripeCsvFileKind.Subscriptions => SubscriptionsRequired,
            StripeCsvFileKind.Products => ProductsRequired,
            StripeCsvFileKind.Prices => PricesRequired,
            _ => throw new ArgumentOutOfRangeException(nameof(fileKind))
        };

    private static IReadOnlyDictionary<StripeLogicalField, string[]> GetOptionalFields(StripeCsvFileKind fileKind) =>
        fileKind switch
        {
            StripeCsvFileKind.Subscriptions => SubscriptionsOptional,
            StripeCsvFileKind.Products => new Dictionary<StripeLogicalField, string[]>(),
            StripeCsvFileKind.Prices => PricesOptional,
            _ => throw new ArgumentOutOfRangeException(nameof(fileKind))
        };

    private static string? FindMatchingHeader(IReadOnlyList<string> headers, IEnumerable<string> aliases)
    {
        foreach (var alias in aliases)
        {
            var match = headers.FirstOrDefault(h =>
                string.Equals(h.Trim(), alias, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
