using BillDrift.Domain.Billing;
using BillDrift.Domain.Import;

namespace BillDrift.Application.Import;

/// <summary>Outcome of ingesting a reseller price list CSV with optional manual overrides.</summary>
public sealed record RetailPricingCsvIngestionResult
{
    /// <summary>Assigned when persisted via orchestration service.</summary>
    public Guid? IngestionId { get; init; }

    /// <summary>SHA-256 fingerprint of catalogue CSV bytes.</summary>
    public required string SourceDocumentId { get; init; }

    /// <summary>UTC timestamp when ingestion completed.</summary>
    public required DateTimeOffset IngestedAt { get; init; }

    /// <summary>Aggregate ingestion outcome.</summary>
    public required IngestionOutcomeStatus Status { get; init; }

    /// <summary>Faithful catalogue rows from the CSV.</summary>
    public required IReadOnlyList<RawPriceListRow> RawCatalogueRows { get; init; }

    /// <summary>Accepted manual override raw entries.</summary>
    public required IReadOnlyList<RawManualPriceEntry> RawManualEntries { get; init; }

    /// <summary>Normalized catalogue-sourced intended prices.</summary>
    public required IReadOnlyList<IntendedPrice> CataloguePrices { get; init; }

    /// <summary>Normalized manual override intended prices.</summary>
    public required IReadOnlyList<IntendedPrice> ManualPrices { get; init; }

    /// <summary>Effective intended prices after pricing strategy merge.</summary>
    public required IReadOnlyList<IntendedPrice> ResolvedPrices { get; init; }

    /// <summary>Per-key resolution metadata.</summary>
    public required IReadOnlyList<PricingResolutionDetail> ResolutionDetails { get; init; }

    /// <summary>Roll-up counts.</summary>
    public required RetailPricingCsvIngestionSummary Summary { get; init; }

    /// <summary>Structured skip and warning entries.</summary>
    public required IReadOnlyList<IngestionLogEntry> LogEntries { get; init; }
}
