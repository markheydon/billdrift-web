namespace BillDrift.Application.Import;

/// <summary>
/// Parses Stripe dashboard CSV exports into raw import records for downstream normalization.
/// </summary>
/// <remarks>
/// Never throws for parse failures — inspect <see cref="StripeCsvIngestionResult.Status"/>.
/// Normalization to <c>StripeBillingItem</c> is a separate stage via <c>IStripeBillingNormalizer</c>.
/// </remarks>
public interface IStripeBillingCsvIngester
{
    /// <summary>
    /// Parses Stripe dashboard CSV exports into raw import records.
    /// </summary>
    /// <param name="request">Bundle of one to three CSV streams; subscriptions required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured raw records plus diagnostic logs.</returns>
    StripeCsvIngestionResult Ingest(
        StripeCsvIngestionRequest request,
        CancellationToken cancellationToken = default);
}
