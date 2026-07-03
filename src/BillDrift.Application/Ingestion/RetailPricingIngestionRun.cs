using BillDrift.Application.Import;
using BillDrift.Domain.Common;

namespace BillDrift.Application.Ingestion;

/// <summary>Queryable index record for a persisted retail pricing ingestion run.</summary>
public sealed record RetailPricingIngestionRun
{
    /// <summary>Unique identifier for this upload run.</summary>
    public required Guid IngestionId { get; init; }

    /// <summary>Always <see cref="ImportSourceKind.GiacomPriceList"/>.</summary>
    public ImportSourceKind SourceKind { get; init; } = ImportSourceKind.GiacomPriceList;

    /// <summary>Original upload filename when provided.</summary>
    public string? OriginalFileName { get; init; }

    /// <summary>SHA-256 hex fingerprint of the catalogue CSV bytes.</summary>
    public required string ContentFingerprint { get; init; }

    /// <summary>UTC timestamp when the upload was received.</summary>
    public required DateTimeOffset UploadedAt { get; init; }

    /// <summary>UTC timestamp when ingestion completed, when known.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Lifecycle status of the ingestion run.</summary>
    public required IngestionRunStatus Status { get; init; }

    /// <summary>Parser roll-up counts.</summary>
    public RetailPricingCsvIngestionSummary? Summary { get; init; }

    /// <summary>Blob path to the uploaded source CSV.</summary>
    public required string SourceBlobPath { get; init; }

    /// <summary>Blob path to the result manifest, set on completion.</summary>
    public string? ResultManifestBlobPath { get; init; }

    /// <summary>Human-readable failure reason when status is failed.</summary>
    public string? FailureReason { get; init; }
}
