using System.Text.Json.Serialization;
using BillDrift.Application.Import;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;

namespace BillDrift.Infrastructure.Ingestion;

/// <summary>Source-generated JSON serialization context for ingestion blob payloads.</summary>
[JsonSerializable(typeof(IngestionManifestDocument))]
[JsonSerializable(typeof(RawRowsBlobDocument))]
[JsonSerializable(typeof(SubscriptionTruthBlobDocument))]
[JsonSerializable(typeof(RawSubscriptionManagementRow))]
[JsonSerializable(typeof(MicrosoftSubscriptionLine))]
[JsonSerializable(typeof(IngestionLogEntry))]
[JsonSerializable(typeof(SubscriptionManagementCsvIngestionSummary))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class IngestionJsonSerializerContext : JsonSerializerContext;

/// <summary>Commit marker written last for an ingestion run.</summary>
public sealed record IngestionManifestDocument(
    Guid IngestionId,
    ImportSourceKind SourceKind,
    string? OriginalFileName,
    string ContentFingerprint,
    DateTimeOffset UploadedAt,
    DateTimeOffset CompletedAt,
    string Status,
    SubscriptionManagementCsvIngestionSummary Summary,
    IngestionManifestBlobs Blobs,
    IngestionManifestContentHashes ContentHashes,
    string? FailureReason = null);

/// <summary>Blob path references in the ingestion manifest.</summary>
public sealed record IngestionManifestBlobs(
    string Source,
    string RawRows,
    string SubscriptionTruth);

/// <summary>SHA-256 content hashes for result blobs.</summary>
public sealed record IngestionManifestContentHashes(
    string RawRows,
    string SubscriptionTruth);

/// <summary>Raw rows blob payload.</summary>
public sealed record RawRowsBlobDocument(
    IReadOnlyList<RawSubscriptionManagementRow> Records,
    IReadOnlyList<IngestionLogEntry> LogEntries);

/// <summary>Normalized subscription truth blob payload.</summary>
public sealed record SubscriptionTruthBlobDocument(
    IReadOnlyList<MicrosoftSubscriptionLine> Records,
    int NormalizationSkipped);
