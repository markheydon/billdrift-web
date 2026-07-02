using BillDrift.Domain.Import.Stripe;

namespace BillDrift.Application.Import;

/// <summary>
/// Outcome of ingesting one or more Stripe dashboard CSV exports into raw import records.
/// Inspect <see cref="Status"/> rather than relying on exceptions for parse failures.
/// </summary>
public sealed record StripeCsvIngestionResult
{
    /// <summary>SHA-256 hex over sorted per-file content hashes.</summary>
    public required string BundleId { get; init; }

    /// <summary>UTC timestamp when ingestion completed.</summary>
    public required DateTimeOffset IngestedAt { get; init; }

    /// <summary>Aggregate outcome across all files and rows.</summary>
    public required IngestionOutcomeStatus Status { get; init; }

    /// <summary>Customers deduplicated from subscription rows.</summary>
    public required IReadOnlyList<RawStripeCustomer> Customers { get; init; }

    /// <summary>Subscriptions deduplicated from subscription rows.</summary>
    public required IReadOnlyList<RawStripeSubscription> Subscriptions { get; init; }

    /// <summary>One record per parsed subscription item row.</summary>
    public required IReadOnlyList<RawStripeSubscriptionItem> SubscriptionItems { get; init; }

    /// <summary>Products from the products CSV, if supplied.</summary>
    public required IReadOnlyList<RawStripeProduct> Products { get; init; }

    /// <summary>Prices from the prices CSV, if supplied.</summary>
    public required IReadOnlyList<RawStripePrice> Prices { get; init; }

    /// <summary>Structured skip and warning entries.</summary>
    public required IReadOnlyList<IngestionLogEntry> LogEntries { get; init; }

    /// <summary>Roll-up extraction counts.</summary>
    public required StripeCsvIngestionSummary Summary { get; init; }

    /// <summary>Per-file fingerprints and row counts.</summary>
    public required IReadOnlyList<StripeCsvSourceFileInfo> SourceFiles { get; init; }
}
