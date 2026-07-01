namespace BillDrift.Application.Import;

/// <summary>
/// Contract for parsing Giacom pre-billing and post-billing PDFs into raw import lines.
/// Implementations live in Infrastructure; callers use this interface to stay decoupled from PDF parsing details.
/// </summary>
public interface IGiacomBillingPdfIngester
{
    /// <summary>
    /// Parses a Giacom pre-billing or post-billing PDF and returns raw import lines.
    /// Never throws for parse failures — inspect <see cref="GiacomPdfIngestionResult.Status"/>.
    /// </summary>
    /// <param name="pdfStream">Readable stream of PDF bytes; the implementation reads from the current position.</param>
    /// <param name="cancellationToken">Token to cancel long-running extraction (e.g. large or multi-page documents).</param>
    /// <returns>A structured result with extracted lines, log entries, and an aggregate <see cref="GiacomPdfIngestionResult.Status"/>.</returns>
    GiacomPdfIngestionResult Ingest(Stream pdfStream, CancellationToken cancellationToken = default);
}
