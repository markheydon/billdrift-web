namespace BillDrift.Application.Import;

/// <summary>
/// Roll-up counts from a Stripe CSV ingestion run for operator dashboards.
/// </summary>
public sealed record StripeCsvIngestionSummary
{
    /// <summary>Subscription items successfully emitted.</summary>
    public int SubscriptionItemsExtracted { get; init; }

    /// <summary>Subscription rows skipped due to parse or validation errors.</summary>
    public int SubscriptionItemsSkipped { get; init; }

    /// <summary>Subscription rows excluded by status filter (not an error).</summary>
    public int SubscriptionsFilteredByStatus { get; init; }

    /// <summary>Products successfully emitted.</summary>
    public int ProductsExtracted { get; init; }

    /// <summary>Product rows skipped.</summary>
    public int ProductsSkipped { get; init; }

    /// <summary>Prices successfully emitted.</summary>
    public int PricesExtracted { get; init; }

    /// <summary>Price rows skipped.</summary>
    public int PricesSkipped { get; init; }

    /// <summary>Rows with missing or inconsistent mapping metadata.</summary>
    public int MetadataWarnings { get; init; }

    /// <summary>Items referencing unknown product or price IDs in the bundle.</summary>
    public int CatalogueWarnings { get; init; }

    /// <summary>Distinct customers extracted from subscription rows.</summary>
    public int CustomersExtracted { get; init; }
}
