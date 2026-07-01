namespace BillDrift.Infrastructure.Import.Giacom;

/// <summary>
/// Intake and parsing guardrails enforced by the Giacom billing PDF pipeline.
/// </summary>
/// <remarks>
/// Limits are applied during intake (file size, page count), extraction (line grouping),
/// column detection (outer column extension), and log emission (snippet truncation).
/// Exceeding document-level limits produces a <c>Failure</c> outcome; weaker header
/// detection continues with best-effort column fallbacks.
/// </remarks>
public static class GiacomIngestionLimits
{
    /// <summary>Maximum PDF file size accepted at intake (20 MB). Exceeding fails the document.</summary>
    public const int MaxFileSizeBytes = 20 * 1024 * 1024;

    /// <summary>Maximum page count before document-level failure at extraction.</summary>
    public const int MaxPageCount = 500;

    /// <summary>Maximum characters retained in ingestion log snippets (security contract).</summary>
    public const int MaxLogSnippetLength = 200;

    /// <summary>Vertical tolerance in PDF points for clustering PdfPig words into <c>PdfTextLine</c> rows.</summary>
    public const double LineGroupingYTolerance = 2.0;

    /// <summary>Horizontal extension in PDF points applied to the leftmost and rightmost detected columns.</summary>
    public const double OuterColumnExtensionPoints = 20.0;
}
