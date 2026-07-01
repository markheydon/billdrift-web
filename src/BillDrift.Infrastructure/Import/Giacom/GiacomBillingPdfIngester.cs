using BillDrift.Application.Import;
using BillDrift.Domain.Import;
using BillDrift.Infrastructure.Import.Giacom.Internal;

namespace BillDrift.Infrastructure.Import.Giacom;

public sealed class GiacomBillingPdfIngester : IGiacomBillingPdfIngester
{
    private readonly PdfTextExtractor _textExtractor = new();
    private readonly ProductLineParser _lineParser = new();
    private readonly RawGiacomBillingLineMapper _lineMapper = new();

    public GiacomPdfIngestionResult Ingest(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);

        cancellationToken.ThrowIfCancellationRequested();

        byte[] pdfBytes;
        try
        {
            pdfBytes = ReadStream(pdfStream);
        }
        catch (Exception)
        {
            return FailureResult(string.Empty, GiacomReportType.Unknown, DateTimeOffset.UtcNow,
                IngestionFailureReason.DocumentUnreadable, "PDF stream could not be read.");
        }

        if (pdfBytes.Length == 0)
        {
            return FailureResult(string.Empty, GiacomReportType.Unknown, DateTimeOffset.UtcNow,
                IngestionFailureReason.EmptyDocument, "PDF stream is empty.");
        }

        if (pdfBytes.Length > GiacomIngestionLimits.MaxFileSizeBytes)
        {
            var docId = DocumentIdentity.ComputeSourceDocumentId(pdfBytes);
            return FailureResult(docId, GiacomReportType.Unknown, DateTimeOffset.UtcNow,
                IngestionFailureReason.FileSizeExceeded, "PDF exceeds maximum allowed file size.");
        }

        var sourceDocumentId = DocumentIdentity.ComputeSourceDocumentId(pdfBytes);
        var ingestedAt = DateTimeOffset.UtcNow;
        var logs = new List<IngestionLogEntry>();

        var extraction = _textExtractor.Extract(pdfBytes, cancellationToken);
        if (extraction.FailureReason is not null)
        {
            return FailureResult(sourceDocumentId, GiacomReportType.Unknown, ingestedAt,
                extraction.FailureReason.Value, DescribeDocumentFailure(extraction.FailureReason.Value), logs);
        }

        var lines = extraction.Lines;
        var firstPageLines = lines.Where(l => l.PageNumber == 1).Select(l => l.Text).ToList();
        var reportType = ReportClassifier.Classify(firstPageLines);

        var columns = ColumnDetector.DetectColumns(lines);
        var blockHeaders = CustomerBlockSegmenter.FindBlockHeaders(lines);

        if (blockHeaders.Count == 0)
        {
            return EmptySuccessResult(sourceDocumentId, reportType, ingestedAt,
                "Document contains no customer billing blocks; import completed with zero lines.",
                logs);
        }

        var rawBlocks = CustomerBlockSegmenter.Segment(lines, columns, _lineParser);
        var outputLines = new List<RawGiacomBillingLine>();
        var linesSkipped = 0;
        var blocksSkipped = 0;
        var warnings = 0;

        foreach (var block in rawBlocks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(block.MexIdRaw))
            {
                blocksSkipped++;
                logs.Add(CreateBlockLog(
                    IngestionFailureReason.MexIdMissing,
                    "Customer block skipped because Mex ID could not be extracted.",
                    block,
                    sourceDocumentId));
                continue;
            }

            if (string.IsNullOrWhiteSpace(block.CustomerNameRaw))
            {
                blocksSkipped++;
                logs.Add(CreateBlockLog(
                    IngestionFailureReason.CustomerNameMissing,
                    "Customer block skipped because customer name could not be extracted.",
                    block,
                    sourceDocumentId));
                continue;
            }

            var mergedLines = ProductNameMerger.Merge(block.ProductLines);

            foreach (var parsedLine in mergedLines)
            {
                var mapResult = _lineMapper.MapLine(parsedLine, block, sourceDocumentId, ingestedAt);
                if (mapResult.SkipLog is not null)
                {
                    linesSkipped++;
                    logs.Add(mapResult.SkipLog);
                    continue;
                }

                if (mapResult.WarningLog is not null)
                {
                    warnings++;
                    logs.Add(mapResult.WarningLog);
                }

                if (mapResult.Line is not null)
                {
                    outputLines.Add(mapResult.Line);
                }
            }
        }

        if (outputLines.Count == 0 && blocksSkipped == rawBlocks.Count)
        {
            return FailureResult(sourceDocumentId, reportType, ingestedAt,
                IngestionFailureReason.NoCustomerBlocksFound,
                "No supplier cost lines could be extracted.", logs);
        }

        if (outputLines.Count == 0 && lines.Count > 0)
        {
            logs.Add(new IngestionLogEntry(
                IngestionLogSeverity.Warning,
                IngestionFailureReason.EmptyDocument,
                "Document parsed successfully but contained zero product lines.",
                null,
                null,
                sourceDocumentId));
        }

        var status = DetermineStatus(outputLines.Count, linesSkipped, blocksSkipped, logs);
        var summary = new GiacomPdfIngestionSummary(
            outputLines.Count,
            linesSkipped,
            blocksSkipped,
            warnings,
            rawBlocks.Count);

        return new GiacomPdfIngestionResult(
            sourceDocumentId,
            reportType,
            ingestedAt,
            status,
            outputLines,
            logs,
            summary);
    }

    private static IngestionOutcomeStatus DetermineStatus(
        int linesExtracted,
        int linesSkipped,
        int blocksSkipped,
        IReadOnlyList<IngestionLogEntry> logs)
    {
        if (linesExtracted == 0)
        {
            if (logs.Any(l => l.Severity == IngestionLogSeverity.Error))
            {
                return IngestionOutcomeStatus.Failure;
            }

            if (linesSkipped > 0 || blocksSkipped > 0)
            {
                return IngestionOutcomeStatus.PartialSuccess;
            }

            return IngestionOutcomeStatus.Success;
        }

        if (linesSkipped > 0 || blocksSkipped > 0 ||
            logs.Any(l => l.Severity == IngestionLogSeverity.Error))
        {
            return IngestionOutcomeStatus.PartialSuccess;
        }

        return IngestionOutcomeStatus.Success;
    }

    private static byte[] ReadStream(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static GiacomPdfIngestionResult EmptySuccessResult(
        string sourceDocumentId,
        GiacomReportType reportType,
        DateTimeOffset ingestedAt,
        string message,
        List<IngestionLogEntry>? existingLogs = null)
    {
        var logs = existingLogs ?? [];
        logs.Add(new IngestionLogEntry(
            IngestionLogSeverity.Warning,
            IngestionFailureReason.EmptyDocument,
            message,
            null,
            null,
            sourceDocumentId));

        return new GiacomPdfIngestionResult(
            sourceDocumentId,
            reportType,
            ingestedAt,
            IngestionOutcomeStatus.Success,
            [],
            logs,
            new GiacomPdfIngestionSummary(0, 0, 0, 0, 0));
    }

    private static GiacomPdfIngestionResult FailureResult(
        string sourceDocumentId,
        GiacomReportType reportType,
        DateTimeOffset ingestedAt,
        IngestionFailureReason reason,
        string message,
        List<IngestionLogEntry>? existingLogs = null)
    {
        var logs = existingLogs ?? [];
        logs.Add(new IngestionLogEntry(
            IngestionLogSeverity.Error,
            reason,
            message,
            null,
            null,
            sourceDocumentId));

        return new GiacomPdfIngestionResult(
            sourceDocumentId,
            reportType,
            ingestedAt,
            IngestionOutcomeStatus.Failure,
            [],
            logs,
            new GiacomPdfIngestionSummary(0, 0, 0, 0, 0));
    }

    private static IngestionLogEntry CreateBlockLog(
        IngestionFailureReason reason,
        string message,
        CustomerBlock block,
        string sourceDocumentId) =>
        new(
            IngestionLogSeverity.Error,
            reason,
            message,
            new IngestionLocation(block.PageNumber, block.BlockIndex, null),
            Truncate(block.CustomerNameRaw ?? block.MexIdRaw),
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

    private static string DescribeDocumentFailure(IngestionFailureReason reason) => reason switch
    {
        IngestionFailureReason.DocumentEncrypted => "PDF is password-protected and cannot be parsed.",
        IngestionFailureReason.PageLimitExceeded => "PDF exceeds maximum allowed page count.",
        IngestionFailureReason.DocumentUnreadable => "PDF could not be read or contains no extractable text.",
        _ => "Document-level ingestion failure."
    };
}
