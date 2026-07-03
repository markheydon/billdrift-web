using BillDrift.Application.Import;
using BillDrift.Domain.Common;

namespace BillDrift.Application.Ingestion;

/// <summary>Queryable index record for a persisted Stripe CSV ingestion run.</summary>
public sealed record StripeCsvIngestionRun
{
    /// <summary>Unique identifier for this upload run.</summary>
    public required Guid IngestionId { get; init; }

    /// <summary>Always <see cref="ImportSourceKind.StripeExport"/>.</summary>
    public ImportSourceKind SourceKind { get; init; } = ImportSourceKind.StripeExport;

    /// <summary>Primary file label for the upload bundle.</summary>
    public string? OriginalFileName { get; init; }

    /// <summary>SHA-256 hex fingerprint of the combined bundle.</summary>
    public required string ContentFingerprint { get; init; }

    /// <summary>UTC timestamp when the upload was received.</summary>
    public required DateTimeOffset UploadedAt { get; init; }

    /// <summary>UTC timestamp when ingestion completed, when known.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Lifecycle status of the ingestion run.</summary>
    public required IngestionRunStatus Status { get; init; }

    /// <summary>Parser roll-up counts.</summary>
    public StripeCsvIngestionSummary? Summary { get; init; }

    /// <summary>Blob path to the uploaded source files.</summary>
    public required string SourceBlobPath { get; init; }

    /// <summary>Blob path to the result manifest, set on completion.</summary>
    public string? ResultManifestBlobPath { get; init; }

    /// <summary>Human-readable failure reason when status is failed.</summary>
    public string? FailureReason { get; init; }
}
