namespace BillDrift.Application.Import;

/// <summary>
/// Per-file metadata captured during Stripe CSV ingestion.
/// </summary>
/// <param name="FileKind">Which export type was processed.</param>
/// <param name="SourceDocumentId">SHA-256 hex fingerprint of the CSV bytes.</param>
/// <param name="OriginalFileName">Caller-supplied filename, if any.</param>
/// <param name="RowCount">Number of data rows processed (excluding header).</param>
public sealed record StripeCsvSourceFileInfo(
    StripeCsvFileKind FileKind,
    string SourceDocumentId,
    string? OriginalFileName,
    int RowCount);
