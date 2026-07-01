namespace BillDrift.Application.Import;

public sealed record IngestionLogEntry(
    IngestionLogSeverity Severity,
    IngestionFailureReason Reason,
    string Message,
    IngestionLocation? Location,
    string? RawSnippet,
    string SourceDocumentId);
