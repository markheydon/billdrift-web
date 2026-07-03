using BillDrift.Infrastructure.Import.Giacom.RetailPricing.Internal;

namespace BillDrift.Infrastructure.Import.Giacom.RetailPricing;

/// <summary>Maps ResellerPricingVsRRP.csv column headers to logical ingestion fields.</summary>
internal static class ResellerPricingCsvHeaderMap
{
    private static readonly IReadOnlyDictionary<ResellerPricingLogicalField, string[]> Required =
        new Dictionary<ResellerPricingLogicalField, string[]>
        {
            [ResellerPricingLogicalField.OfferId] = ["Offer ID", "OfferId", "Offer_Id", "Microsoft Offer ID", "Offer"],
            [ResellerPricingLogicalField.SkuId] = ["SKU ID", "SkuId", "SKU_Id", "Sku ID", "SKU", "Product SKU"],
            [ResellerPricingLogicalField.Term] = ["Term", "Contract Term", "Duration", "Commitment Term", "Billing Term"],
            [ResellerPricingLogicalField.Frequency] = ["Frequency", "Billing Frequency", "Bill Frequency", "Payment Frequency", "Billing Cycle"],
            [ResellerPricingLogicalField.Wholesale] = ["Wholesale", "Wholesale Price", "Cost", "Buy Price", "Partner Price"],
            [ResellerPricingLogicalField.Rrp] = ["RRP", "Rrp", "Recommended Retail Price", "Retail Price", "List Price", "Sell Price", "ERP"]
        };

    private static readonly IReadOnlyDictionary<ResellerPricingLogicalField, string[]> Optional =
        new Dictionary<ResellerPricingLogicalField, string[]>
        {
            [ResellerPricingLogicalField.Margin] = ["Margin", "Margin Amount", "Absolute Margin", "Profit"],
            [ResellerPricingLogicalField.MarginPercent] = ["Margin %", "Margin Percent", "Margin Percentage", "MarginPct", "GP %"],
            [ResellerPricingLogicalField.Status] = ["Status", "Product Status", "Availability", "State"],
            [ResellerPricingLogicalField.Platform] = ["Platform", "Commerce Platform", "NCE/Legacy", "Product Platform", "CSP Platform"],
            [ResellerPricingLogicalField.Currency] = ["Currency", "Currency Code", "Curr"]
        };

    public static IReadOnlyDictionary<ResellerPricingLogicalField, string> BuildFieldToHeaderMap(
        IReadOnlyList<string> headers)
    {
        var map = new Dictionary<ResellerPricingLogicalField, string>();

        foreach (var (field, aliases) in Required)
        {
            var matched = FindMatchingHeader(headers, aliases);
            if (matched is not null)
            {
                map[field] = matched;
            }
        }

        foreach (var (field, aliases) in Optional)
        {
            var matched = FindMatchingHeader(headers, aliases);
            if (matched is not null)
            {
                map[field] = matched;
            }
        }

        return map;
    }

    public static IReadOnlyList<ResellerPricingLogicalField> GetMissingRequiredFields(IReadOnlyList<string> headers)
    {
        var missing = new List<ResellerPricingLogicalField>();
        foreach (var (field, aliases) in Required)
        {
            if (FindMatchingHeader(headers, aliases) is null)
            {
                missing.Add(field);
            }
        }

        return missing;
    }

    public static string? GetFieldValue(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<ResellerPricingLogicalField, string> fieldMap,
        ResellerPricingLogicalField field) =>
        fieldMap.TryGetValue(field, out var header) && row.TryGetValue(header, out var value)
            ? value
            : null;

    public static ParsedResellerPricingRow ToParsedRow(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<ResellerPricingLogicalField, string> fieldMap,
        int rowNumber)
    {
        var fields = new Dictionary<ResellerPricingLogicalField, string?>();
        foreach (ResellerPricingLogicalField field in Enum.GetValues<ResellerPricingLogicalField>())
        {
            fields[field] = GetFieldValue(row, fieldMap, field);
        }

        return new ParsedResellerPricingRow
        {
            RowNumber = rowNumber,
            Fields = fields
        };
    }

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
