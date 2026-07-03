using System.Globalization;
using BillDrift.Application.Import;
using BillDrift.Application.Normalization;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;
using BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement.Internal;

namespace BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement;

internal sealed class RawSubscriptionManagementRowMapper
{
    public sealed record MapResult(
        RawSubscriptionManagementRow? Row,
        IngestionLogEntry? SkipLog,
        IngestionLogEntry? WarningLog);

    public MapResult Map(
        ParsedSubscriptionManagementRow parsed,
        string sourceDocumentId)
    {
        var mexId = GetField(parsed, SubscriptionManagementLogicalField.MexId);
        if (string.IsNullOrWhiteSpace(mexId))
        {
            return Skip(parsed, sourceDocumentId, IngestionFailureReason.MexIdMissing,
                "Row skipped because Mex ID is missing.", mexId);
        }

        var licences = GetField(parsed, SubscriptionManagementLogicalField.Licences);
        if (!SubscriptionManagementNormalizer.TryParseLicenceCount(licences, out _))
        {
            return Skip(parsed, sourceDocumentId, IngestionFailureReason.LicenceCountUnparseable,
                "Row skipped because licence count could not be parsed.", licences);
        }

        var price = GetField(parsed, SubscriptionManagementLogicalField.Price);
        if (!string.IsNullOrWhiteSpace(price) &&
            !SubscriptionManagementNormalizer.TryParseMoney(price, out _))
        {
            return Skip(parsed, sourceDocumentId, IngestionFailureReason.PriceUnparseable,
                "Row skipped because price could not be parsed.", price);
        }

        IngestionLogEntry? warning = null;
        var offerId = GetField(parsed, SubscriptionManagementLogicalField.OfferId) ?? string.Empty;
        var skuId = GetField(parsed, SubscriptionManagementLogicalField.SkuId) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(offerId) || string.IsNullOrWhiteSpace(skuId))
        {
            warning = CreateLog(
                IngestionLogSeverity.Warning,
                IngestionFailureReason.CommercialKeyMissing,
                "Row emitted with missing offer ID or SKU ID.",
                parsed,
                sourceDocumentId,
                $"{offerId}/{skuId}");
        }

        var lineKey = parsed.RowNumber.ToString(CultureInfo.InvariantCulture);
        var id = RawImportId.Create(ImportSourceKind.GiacomSubscriptionManagement, sourceDocumentId, lineKey);

        var row = new RawSubscriptionManagementRow(
            id,
            GetField(parsed, SubscriptionManagementLogicalField.CustomerName) ?? string.Empty,
            mexId.Trim(),
            TrimOrNull(GetField(parsed, SubscriptionManagementLogicalField.TenantId)),
            offerId,
            skuId,
            licences ?? string.Empty,
            GetField(parsed, SubscriptionManagementLogicalField.Term) ?? string.Empty,
            GetField(parsed, SubscriptionManagementLogicalField.Frequency) ?? string.Empty,
            TrimOrNull(GetField(parsed, SubscriptionManagementLogicalField.RenewalDate)),
            GetField(parsed, SubscriptionManagementLogicalField.Status) ?? string.Empty,
            TrimOrNull(GetField(parsed, SubscriptionManagementLogicalField.SupplierSubscriptionId)),
            sourceDocumentId,
            parsed.RowNumber,
            TrimOrNull(GetField(parsed, SubscriptionManagementLogicalField.Service)),
            TrimOrNull(GetField(parsed, SubscriptionManagementLogicalField.ProductName)),
            TrimOrNull(GetField(parsed, SubscriptionManagementLogicalField.ProductType)),
            TrimOrNull(GetField(parsed, SubscriptionManagementLogicalField.IsNce)),
            TrimOrNull(GetField(parsed, SubscriptionManagementLogicalField.IsTrial)),
            TrimOrNull(price),
            TrimOrNull(GetField(parsed, SubscriptionManagementLogicalField.Erp)),
            TrimOrNull(GetField(parsed, SubscriptionManagementLogicalField.EndOfTermAction)),
            TrimOrNull(GetField(parsed, SubscriptionManagementLogicalField.CancellableUntil)),
            TrimOrNull(GetField(parsed, SubscriptionManagementLogicalField.MigrationToNce)),
            TrimOrNull(GetField(parsed, SubscriptionManagementLogicalField.AssignedLicences)));

        return new MapResult(row, null, warning);
    }

    private static MapResult Skip(
        ParsedSubscriptionManagementRow parsed,
        string sourceDocumentId,
        IngestionFailureReason reason,
        string message,
        string? snippet) =>
        new(null, CreateLog(IngestionLogSeverity.Error, reason, message, parsed, sourceDocumentId, snippet), null);

    private static string? GetField(ParsedSubscriptionManagementRow row, SubscriptionManagementLogicalField field) =>
        row.Fields.TryGetValue(field, out var value) ? value : null;

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IngestionLogEntry CreateLog(
        IngestionLogSeverity severity,
        IngestionFailureReason reason,
        string message,
        ParsedSubscriptionManagementRow parsed,
        string sourceDocumentId,
        string? snippet) =>
        new(
            severity,
            reason,
            message,
            new IngestionLocation(0, 0, parsed.RowNumber),
            CapSnippet(snippet),
            sourceDocumentId);

    private static string? CapSnippet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 200 ? trimmed : trimmed[..200];
    }
}
