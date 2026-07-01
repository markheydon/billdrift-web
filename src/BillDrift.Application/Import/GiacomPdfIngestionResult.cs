using BillDrift.Domain.Import;

namespace BillDrift.Application.Import;

/// <summary>
/// Outcome of ingesting a single Giacom billing PDF, including extracted raw lines and operator diagnostics.
/// Inspect <see cref="Status"/> rather than relying on exceptions for parse failures.
/// </summary>
/// <param name="SourceDocumentId">SHA-256 hex fingerprint of the PDF bytes; ties every line and log entry to one source document.</param>
/// <param name="ReportType">Detected report variant (pre-billing, post-billing, or unknown).</param>
/// <param name="IngestedAt">UTC timestamp when the ingestion pipeline completed for this document.</param>
/// <param name="Status">Aggregate outcome: all lines extracted, partial extraction, or total failure.</param>
/// <param name="Lines">Successfully extracted <see cref="RawGiacomBillingLine"/> records; empty when <see cref="Status"/> is <see cref="IngestionOutcomeStatus.Failure"/>.</param>
/// <param name="LogEntries">Structured skip and warning entries for operator review and automation.</param>
/// <param name="Summary">Roll-up counts for lines, blocks, warnings, and customer blocks.</param>
public sealed record GiacomPdfIngestionResult(
    string SourceDocumentId,
    GiacomReportType ReportType,
    DateTimeOffset IngestedAt,
    IngestionOutcomeStatus Status,
    IReadOnlyList<RawGiacomBillingLine> Lines,
    IReadOnlyList<IngestionLogEntry> LogEntries,
    GiacomPdfIngestionSummary Summary);
