using BillDrift.Application.Import;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;
using BillDrift.Infrastructure.Import.Giacom.Internal;

namespace BillDrift.Infrastructure.Import.Giacom;

internal sealed class RawGiacomBillingLineMapper
{
    public sealed record MapResult(
        RawGiacomBillingLine? Line,
        IngestionLogEntry? SkipLog,
        IngestionLogEntry? WarningLog);

    public MapResult MapLine(
        ParsedProductLine parsedLine,
        CustomerBlock block,
        string sourceDocumentId,
        DateTimeOffset extractedAt)
    {
        // Line-level skip tier: missing quantity or cost prevents RawGiacomBillingLine emission.
        if (string.IsNullOrWhiteSpace(parsedLine.QuantityRaw))
        {
            return Skip(parsedLine, block, sourceDocumentId, IngestionFailureReason.QuantityUnparseable,
                "Product line quantity is missing or unparseable.");
        }

        if (string.IsNullOrWhiteSpace(parsedLine.LineCostRaw))
        {
            return Skip(parsedLine, block, sourceDocumentId, IngestionFailureReason.LineCostUnparseable,
                "Product line cost is missing or unparseable.");
        }

        IngestionLogEntry? warning = null;
        var periodStart = parsedLine.PeriodStartRaw;
        var periodEnd = parsedLine.PeriodEndRaw;

        if (HasPeriodText(parsedLine) && periodStart is null && periodEnd is null)
        {
            warning = CreateLog(
                IngestionLogSeverity.Warning,
                IngestionFailureReason.PeriodUnparseable,
                "Billing period text could not be parsed; line emitted with null period fields.",
                parsedLine,
                sourceDocumentId,
                BuildPeriodSnippet(parsedLine));
        }

        var mexIdRaw = (block.MexIdRaw ?? string.Empty).Trim();
        var lineKey = LineKeyResolver.Resolve(parsedLine);
        var id = RawImportId.Create(ImportSourceKind.GiacomBillingPdf, sourceDocumentId, lineKey);

        var line = new RawGiacomBillingLine(
            id,
            mexIdRaw,
            parsedLine.ProductNameRaw,
            parsedLine.QuantityRaw.Trim(),
            parsedLine.ChargeTypeRaw ?? "Recurring",
            periodStart,
            periodEnd,
            parsedLine.LineCostRaw.Trim(),
            parsedLine.SupplierReferenceIds,
            sourceDocumentId,
            extractedAt);

        return new MapResult(line, null, warning);
    }

    private static MapResult Skip(
        ParsedProductLine parsedLine,
        CustomerBlock block,
        string sourceDocumentId,
        IngestionFailureReason reason,
        string message) =>
        new(null, CreateLog(
            IngestionLogSeverity.Error,
            reason,
            message,
            parsedLine,
            sourceDocumentId,
            Truncate(parsedLine.ProductNameRaw)), null);

    private static bool HasPeriodText(ParsedProductLine line) =>
        !string.IsNullOrWhiteSpace(line.PeriodStartRaw) ||
        !string.IsNullOrWhiteSpace(line.PeriodEndRaw);

    private static string? BuildPeriodSnippet(ParsedProductLine line) =>
        Truncate($"{line.PeriodStartRaw} {line.PeriodEndRaw}".Trim());

    private static IngestionLogEntry CreateLog(
        IngestionLogSeverity severity,
        IngestionFailureReason reason,
        string message,
        ParsedProductLine parsedLine,
        string sourceDocumentId,
        string? snippet) =>
        new(
            severity,
            reason,
            message,
            new IngestionLocation(parsedLine.PageNumber, parsedLine.BlockIndex, parsedLine.LineIndex),
            snippet,
            sourceDocumentId);

    private static string? Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return text.Length <= GiacomIngestionLimits.MaxLogSnippetLength
            ? text
            : text[..GiacomIngestionLimits.MaxLogSnippetLength];
    }
}
