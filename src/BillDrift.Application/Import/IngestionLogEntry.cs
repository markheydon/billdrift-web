namespace BillDrift.Application.Import;

/// <summary>
/// A single diagnostic entry from Giacom PDF ingestion, capturing why a line or block was skipped or warned about.
/// </summary>
/// <param name="Severity">Whether this entry is a non-fatal warning or a blocking error.</param>
/// <param name="Reason">Machine-readable <see cref="IngestionFailureReason"/> for automation and filtering.</param>
/// <param name="Message">Human-readable description for operator review.</param>
/// <param name="Location">Position in the PDF where the issue occurred; <c>null</c> for document-level failures.</param>
/// <param name="RawSnippet">Truncated source text (max 200 characters) surrounding the failure, if available.</param>
/// <param name="SourceDocumentId">SHA-256 hex fingerprint of the source PDF, matching <see cref="GiacomPdfIngestionResult.SourceDocumentId"/>.</param>
public sealed record IngestionLogEntry(
    IngestionLogSeverity Severity,
    IngestionFailureReason Reason,
    string Message,
    IngestionLocation? Location,
    string? RawSnippet,
    string SourceDocumentId);
