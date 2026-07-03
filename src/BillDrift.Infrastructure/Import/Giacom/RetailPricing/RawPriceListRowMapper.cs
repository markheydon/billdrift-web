using System.Globalization;
using BillDrift.Application.Import;
using BillDrift.Application.Normalization;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;
using BillDrift.Infrastructure.Import.Giacom.RetailPricing.Internal;

namespace BillDrift.Infrastructure.Import.Giacom.RetailPricing;

internal sealed class RawPriceListRowMapper
{
    public sealed record MapResult(
        RawPriceListRow? Row,
        IngestionLogEntry? SkipLog,
        IngestionLogEntry? WarningLog);

    public MapResult Map(ParsedResellerPricingRow parsed, string sourceDocumentId)
    {
        var offerId = GetField(parsed, ResellerPricingLogicalField.OfferId) ?? string.Empty;
        var skuId = GetField(parsed, ResellerPricingLogicalField.SkuId) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(offerId) && string.IsNullOrWhiteSpace(skuId))
        {
            return Skip(parsed, sourceDocumentId, IngestionFailureReason.CommercialKeyMissing,
                "Row skipped because both offer ID and SKU ID are missing.", $"{offerId}/{skuId}");
        }

        var term = GetField(parsed, ResellerPricingLogicalField.Term);
        if (string.IsNullOrWhiteSpace(term) || !PriceListNormalizer.TryParseTerm(term, out _))
        {
            return Skip(parsed, sourceDocumentId, IngestionFailureReason.TermUnparseable,
                "Row skipped because term could not be parsed.", term);
        }

        var frequency = GetField(parsed, ResellerPricingLogicalField.Frequency);
        if (string.IsNullOrWhiteSpace(frequency) || !PriceListNormalizer.TryParseFrequency(frequency, out _))
        {
            return Skip(parsed, sourceDocumentId, IngestionFailureReason.FrequencyUnparseable,
                "Row skipped because billing frequency could not be parsed.", frequency);
        }

        var wholesale = GetField(parsed, ResellerPricingLogicalField.Wholesale);
        if (string.IsNullOrWhiteSpace(wholesale) ||
            !SubscriptionManagementNormalizer.TryParseMoney(wholesale, out _))
        {
            return Skip(parsed, sourceDocumentId, IngestionFailureReason.WholesaleUnparseable,
                "Row skipped because wholesale price could not be parsed.", wholesale);
        }

        var rrp = GetField(parsed, ResellerPricingLogicalField.Rrp);
        if (string.IsNullOrWhiteSpace(rrp) ||
            !SubscriptionManagementNormalizer.TryParseMoney(rrp, out _))
        {
            return Skip(parsed, sourceDocumentId, IngestionFailureReason.RrpUnparseable,
                "Row skipped because RRP could not be parsed.", rrp);
        }

        var currency = GetField(parsed, ResellerPricingLogicalField.Currency);
        if (!string.IsNullOrWhiteSpace(currency) &&
            !string.Equals(currency.Trim(), "GBP", StringComparison.OrdinalIgnoreCase))
        {
            return Skip(parsed, sourceDocumentId, IngestionFailureReason.UnsupportedCurrency,
                "Row skipped because only GBP is supported in v1.", currency);
        }

        IngestionLogEntry? warning = null;
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

        var platformRaw = TrimOrNull(GetField(parsed, ResellerPricingLogicalField.Platform));
        if (platformRaw is not null && !PlatformClassifier.IsRecognised(platformRaw))
        {
            warning ??= CreateLog(
                IngestionLogSeverity.Warning,
                IngestionFailureReason.PlatformUnrecognised,
                "Unrecognised platform value; treated as unknown.",
                parsed,
                sourceDocumentId,
                platformRaw);
        }

        var lineKey = parsed.RowNumber.ToString(CultureInfo.InvariantCulture);
        var id = RawImportId.Create(ImportSourceKind.GiacomPriceList, sourceDocumentId, lineKey);

        var row = new RawPriceListRow(
            id,
            offerId,
            skuId,
            term,
            frequency,
            wholesale,
            rrp,
            TrimOrNull(GetField(parsed, ResellerPricingLogicalField.Margin)),
            TrimOrNull(GetField(parsed, ResellerPricingLogicalField.MarginPercent)),
            GetField(parsed, ResellerPricingLogicalField.Status) ?? string.Empty,
            platformRaw,
            TrimOrNull(currency),
            sourceDocumentId,
            parsed.RowNumber);

        return new MapResult(row, null, warning);
    }

    private static MapResult Skip(
        ParsedResellerPricingRow parsed,
        string sourceDocumentId,
        IngestionFailureReason reason,
        string message,
        string? snippet) =>
        new(null, CreateLog(IngestionLogSeverity.Error, reason, message, parsed, sourceDocumentId, snippet), null);

    private static string? GetField(ParsedResellerPricingRow row, ResellerPricingLogicalField field) =>
        row.Fields.TryGetValue(field, out var value) ? value : null;

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IngestionLogEntry CreateLog(
        IngestionLogSeverity severity,
        IngestionFailureReason reason,
        string message,
        ParsedResellerPricingRow parsed,
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
