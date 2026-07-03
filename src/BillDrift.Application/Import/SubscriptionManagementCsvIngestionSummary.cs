namespace BillDrift.Application.Import;

/// <summary>
/// Roll-up counts from a Subscription Management CSV ingestion run.
/// </summary>
public sealed record SubscriptionManagementCsvIngestionSummary
{
    /// <summary>Total data rows read from the CSV.</summary>
    public int RowsRead { get; init; }

    /// <summary>Rows emitted into <see cref="SubscriptionManagementCsvIngestionResult.RawRows"/>.</summary>
    public int RowsEmitted { get; init; }

    /// <summary>Rows skipped due to validation or parse failures.</summary>
    public int RowsSkipped { get; init; }

    /// <summary>Rows excluded by the Microsoft 365 / CSP scope filter.</summary>
    public int RowsExcludedByScope { get; init; }

    /// <summary>Raw rows that could not be normalized into subscription truth lines.</summary>
    public int NormalizationSkipped { get; init; }

    /// <summary>Rows with a missing offer ID or SKU ID warning.</summary>
    public int CommercialKeyWarnings { get; init; }

    /// <summary>Rows included despite ambiguous product scope classification.</summary>
    public int ScopeAmbiguityWarnings { get; init; }
}
