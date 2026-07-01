namespace BillDrift.Application.Import;

public interface IGiacomBillingPdfIngester
{
    /// <summary>
    /// Parses a Giacom pre-billing or post-billing PDF and returns raw import lines.
    /// Never throws for parse failures — inspect <see cref="GiacomPdfIngestionResult.Status"/>.
    /// </summary>
    GiacomPdfIngestionResult Ingest(Stream pdfStream, CancellationToken cancellationToken = default);
}
