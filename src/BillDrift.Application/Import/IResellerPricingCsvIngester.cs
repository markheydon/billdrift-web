namespace BillDrift.Application.Import;

/// <summary>
/// Parses Giacom <c>ResellerPricingVsRRP.csv</c> exports into raw rows and resolved intended prices.
/// </summary>
public interface IResellerPricingCsvIngester
{
    /// <summary>
    /// Parses the catalogue CSV and optional manual overrides into intended pricing records.
    /// Never throws for parse failures — inspect <see cref="RetailPricingCsvIngestionResult.Status"/>.
    /// </summary>
    RetailPricingCsvIngestionResult Ingest(
        RetailPricingCsvIngestionRequest request,
        CancellationToken cancellationToken = default);
}
