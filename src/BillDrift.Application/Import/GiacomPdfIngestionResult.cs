using BillDrift.Domain.Import;

namespace BillDrift.Application.Import;

public sealed record GiacomPdfIngestionResult(
    string SourceDocumentId,
    GiacomReportType ReportType,
    DateTimeOffset IngestedAt,
    IngestionOutcomeStatus Status,
    IReadOnlyList<RawGiacomBillingLine> Lines,
    IReadOnlyList<IngestionLogEntry> LogEntries,
    GiacomPdfIngestionSummary Summary);
