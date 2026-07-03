namespace BillDrift.Application.Import;

/// <summary>Roll-up counts for a retail pricing ingestion run.</summary>
public sealed record RetailPricingCsvIngestionSummary
{
    /// <summary>Total catalogue data rows read from CSV.</summary>
    public int CatalogueRowsRead { get; init; }

    /// <summary>Catalogue rows emitted as raw price list rows.</summary>
    public int CatalogueRowsEmitted { get; init; }

    /// <summary>Catalogue rows skipped by validation.</summary>
    public int CatalogueRowsSkipped { get; init; }

    /// <summary>Manual override requests submitted.</summary>
    public int ManualOverridesSubmitted { get; init; }

    /// <summary>Manual overrides accepted and normalized.</summary>
    public int ManualOverridesAccepted { get; init; }

    /// <summary>Manual overrides rejected by validation.</summary>
    public int ManualOverridesRejected { get; init; }

    /// <summary>Warnings for duplicate commercial keys within the catalogue file.</summary>
    public int DuplicateKeyWarnings { get; init; }

    /// <summary>Commercial keys where manual override beat catalogue.</summary>
    public int OverrideWinsCount { get; init; }

    /// <summary>Commercial keys resolved from catalogue only.</summary>
    public int CatalogueOnlyCount { get; init; }

    /// <summary>Distinct commercial keys in resolved output.</summary>
    public int ResolvedPriceCount { get; init; }

    /// <summary>Catalogue rows that failed normalization.</summary>
    public int NormalizationSkipped { get; init; }
}
