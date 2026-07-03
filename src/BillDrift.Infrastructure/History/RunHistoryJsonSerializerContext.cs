using System.Text.Json.Serialization;
using BillDrift.Domain.Billing;
using BillDrift.Domain.History;
using BillDrift.Domain.Mapping;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Infrastructure.History;

/// <summary>Source-generated JSON serialization context for run history blobs.</summary>
[JsonSerializable(typeof(InputBlobDocument<SupplierCostLine>))]
[JsonSerializable(typeof(InputBlobDocument<MicrosoftSubscriptionLine>))]
[JsonSerializable(typeof(InputBlobDocument<IntendedPrice>))]
[JsonSerializable(typeof(InputBlobDocument<StripeBillingItem>))]
[JsonSerializable(typeof(InputBlobDocument<ProductMapping>))]
[JsonSerializable(typeof(ResultsBlobDocument<EntityMatchGroup>))]
[JsonSerializable(typeof(ResultsBlobDocument<Mismatch>))]
[JsonSerializable(typeof(ResultsBlobDocument<ProposedChange>))]
[JsonSerializable(typeof(RunManifestDocument))]
[JsonSerializable(typeof(RunComparisonReport))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class RunHistoryJsonSerializerContext : JsonSerializerContext;

/// <summary>Input blob wrapper with domain records.</summary>
public sealed record InputBlobDocument<T>(
    string Domain,
    SourceMetadataDocument? SourceMetadata,
    IReadOnlyList<T> Records,
    string? ContentHash = null);

/// <summary>Results blob wrapper.</summary>
public sealed record ResultsBlobDocument<T>(IReadOnlyList<T> Records, string? ContentHash = null);

/// <summary>Source file metadata in input blobs.</summary>
public sealed record SourceMetadataDocument(
    string? FileName,
    DateTimeOffset? UploadedAt,
    string? ContentFingerprint,
    BillingPeriodDocument? BillingPeriod);

/// <summary>Billing period in JSON documents.</summary>
public sealed record BillingPeriodDocument(string Start, string End);

/// <summary>Run archive manifest document.</summary>
public sealed record RunManifestDocument(
    int SchemaVersion,
    Guid RunId,
    DateTimeOffset ArchivedAt,
    BillingPeriodDocument BillingPeriod,
    MappingVersionDocument MappingVersion,
    Dictionary<string, ManifestInputEntry> Inputs,
    ManifestResultsSection Results,
    ManifestSummaryMetrics SummaryMetrics);

/// <summary>Mapping version in manifest.</summary>
public sealed record MappingVersionDocument(
    string VersionId,
    string ContentHash,
    string EffectiveDate,
    string? Label = null);

/// <summary>Input entry in manifest.</summary>
public sealed record ManifestInputEntry(
    bool Present,
    string? BlobPath = null,
    string? ContentHash = null,
    int RecordCount = 0);

/// <summary>Results section in manifest.</summary>
public sealed record ManifestResultsSection(
    ManifestBlobRef MatchGroups,
    ManifestBlobRef Mismatches,
    ManifestBlobRef ProposedChanges);

/// <summary>Blob reference in manifest.</summary>
public sealed record ManifestBlobRef(string BlobPath, string ContentHash);

/// <summary>Summary metrics in manifest.</summary>
public sealed record ManifestSummaryMetrics(
    int MatchGroupCount,
    int MismatchCount,
    int ProposedChangeCount,
    bool CleanRun);
