using BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement.Internal;

namespace BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement;

/// <summary>
/// Maps Giacom Subscription Management CSV column headers to logical ingestion fields via alias registry.
/// </summary>
internal static class SubscriptionManagementCsvHeaderMap
{
    private static readonly IReadOnlyDictionary<SubscriptionManagementLogicalField, string[]> Required =
        new Dictionary<SubscriptionManagementLogicalField, string[]>
        {
            [SubscriptionManagementLogicalField.MexId] = ["Mex ID", "MEX ID", "MexId", "mex_id", "Sub Account", "Sub Account ID"],
            [SubscriptionManagementLogicalField.OfferId] = ["Offer ID", "Offer Id", "offer_id", "OfferID", "OfferId"],
            [SubscriptionManagementLogicalField.SkuId] = ["SKU ID", "Sku ID", "Sku Id", "sku_id", "SKU", "SkuID"],
            [SubscriptionManagementLogicalField.Licences] = ["Licences", "Licenses", "License Count", "Qty", "Quantity", "Seats"],
            [SubscriptionManagementLogicalField.Status] = ["Status", "Subscription Status", "Sub Status"]
        };

    private static readonly IReadOnlyDictionary<SubscriptionManagementLogicalField, string[]> Optional =
        new Dictionary<SubscriptionManagementLogicalField, string[]>
        {
            [SubscriptionManagementLogicalField.CustomerName] = ["Customer", "Customer Name", "Account Name", "Company"],
            [SubscriptionManagementLogicalField.TenantId] = ["Tenant ID", "Tenant Id", "tenant_id", "Microsoft Tenant ID", "Tenant"],
            [SubscriptionManagementLogicalField.Service] = ["Service", "Service Name", "Product Family"],
            [SubscriptionManagementLogicalField.ProductName] = ["Product", "Product Name", "Subscription", "SKU Name"],
            [SubscriptionManagementLogicalField.ProductType] = ["Product Type", "ProductType", "Type", "Billing Type"],
            [SubscriptionManagementLogicalField.SupplierSubscriptionId] = ["Subscription ID", "Subscription Id", "Sub ID", "Giacom Subscription ID", "Supplier Reference"],
            [SubscriptionManagementLogicalField.Term] = ["Term", "Term Duration", "Commitment Term", "Billing Term"],
            [SubscriptionManagementLogicalField.Frequency] = ["Billing Frequency", "Frequency", "Billing Cycle", "Payment Frequency"],
            [SubscriptionManagementLogicalField.RenewalDate] = ["Renewal Date", "Next Renewal", "Renewal", "Anniversary Date"],
            [SubscriptionManagementLogicalField.EndOfTermAction] = ["End of Term Action", "End Of Term Action", "Auto Renew", "Cancellation Policy"],
            [SubscriptionManagementLogicalField.IsNce] = ["NCE", "Is NCE", "NCE Flag", "New Commerce Experience"],
            [SubscriptionManagementLogicalField.IsTrial] = ["Trial", "Is Trial", "Trial Flag", "Trial Subscription"],
            [SubscriptionManagementLogicalField.CancellableUntil] = ["Cancellable Until", "Cancel Until", "Cancellation Deadline"],
            [SubscriptionManagementLogicalField.MigrationToNce] = ["Migration to NCE", "NCE Migration", "Migrate to NCE"],
            [SubscriptionManagementLogicalField.AssignedLicences] = ["Assigned Licences", "Assigned Licenses", "Assigned", "Assigned Seats"],
            [SubscriptionManagementLogicalField.Price] = ["Price", "Unit Price", "Sell Price", "Customer Price"],
            [SubscriptionManagementLogicalField.Erp] = ["ERP", "Erp", "Estimated Retail Price", "RRP", "List Price"]
        };

    public static IReadOnlyDictionary<SubscriptionManagementLogicalField, string> BuildFieldToHeaderMap(
        IReadOnlyList<string> headers)
    {
        var map = new Dictionary<SubscriptionManagementLogicalField, string>();

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

    public static IReadOnlyList<SubscriptionManagementLogicalField> GetMissingRequiredFields(
        IReadOnlyList<string> headers)
    {
        var missing = new List<SubscriptionManagementLogicalField>();
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
        IReadOnlyDictionary<SubscriptionManagementLogicalField, string> fieldMap,
        SubscriptionManagementLogicalField field) =>
        fieldMap.TryGetValue(field, out var header) && row.TryGetValue(header, out var value)
            ? value
            : null;

    public static ParsedSubscriptionManagementRow ToParsedRow(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<SubscriptionManagementLogicalField, string> fieldMap,
        int rowNumber)
    {
        var fields = new Dictionary<SubscriptionManagementLogicalField, string?>();
        foreach (SubscriptionManagementLogicalField field in Enum.GetValues<SubscriptionManagementLogicalField>())
        {
            fields[field] = GetFieldValue(row, fieldMap, field);
        }

        return new ParsedSubscriptionManagementRow
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
