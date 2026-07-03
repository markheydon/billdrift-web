using BillDrift.Domain.Billing;
using BillDrift.Domain.Import;

namespace BillDrift.Application.Import;

/// <summary>
/// Result of parsing a Giacom Subscription Management CSV export.
/// </summary>
public sealed record SubscriptionManagementCsvIngestionResult
{
    /// <summary>Correlates blob and table artifacts for upload workflows.</summary>
    public Guid IngestionId { get; init; } = Guid.NewGuid();

    /// <summary>SHA-256 hex fingerprint of the CSV bytes.</summary>
    public required string SourceDocumentId { get; init; }

    /// <summary>UTC timestamp when ingestion completed.</summary>
    public required DateTimeOffset IngestedAt { get; init; }

    /// <summary>Aggregate outcome of the ingestion run.</summary>
    public required IngestionOutcomeStatus Status { get; init; }

    /// <summary>Faithful raw rows from qualifying in-scope CSV lines.</summary>
    public required IReadOnlyList<RawSubscriptionManagementRow> RawRows { get; init; }

    /// <summary>Normalized subscription truth lines ready for reconciliation.</summary>
    public required IReadOnlyList<MicrosoftSubscriptionLine> SubscriptionLines { get; init; }

    /// <summary>Structured diagnostic entries for operator review.</summary>
    public required IReadOnlyList<IngestionLogEntry> LogEntries { get; init; }

    /// <summary>Roll-up counts for dashboards and API responses.</summary>
    public required SubscriptionManagementCsvIngestionSummary Summary { get; init; }

    /// <summary>Source file metadata.</summary>
    public required SubscriptionManagementSourceFileInfo SourceFile { get; init; }
}
