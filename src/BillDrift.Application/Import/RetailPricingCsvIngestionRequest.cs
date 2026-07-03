namespace BillDrift.Application.Import;

/// <summary>Request to ingest a Giacom reseller price list CSV plus optional manual overrides.</summary>
public sealed record RetailPricingCsvIngestionRequest(
    Stream CatalogueContent,
    string? OriginalFileName = null,
    IReadOnlyList<ManualPriceOverrideRequest>? ManualOverrides = null,
    RetailPricingCsvIngestionOptions? Options = null);
