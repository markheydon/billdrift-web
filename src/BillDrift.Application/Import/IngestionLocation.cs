namespace BillDrift.Application.Import;

/// <summary>
/// Pinpoints where in a Giacom PDF an ingestion issue occurred, helping operators locate problems in the source document.
/// </summary>
/// <param name="PageNumber">One-based page number within the PDF.</param>
/// <param name="BlockIndex">Zero-based index of the customer block on the page.</param>
/// <param name="LineIndex">Zero-based index of the billing line within the block, or <c>null</c> for block- or document-level issues.</param>
public sealed record IngestionLocation(
    int PageNumber,
    int BlockIndex,
    int? LineIndex);
