using System.Text.Json.Serialization;
using BillDrift.Application.Import;
using BillDrift.Domain.Billing;
using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;

namespace BillDrift.Infrastructure.Ingestion;

/// <summary>Source-generated JSON serialization context for ingestion blob payloads.</summary>
[JsonSerializable(typeof(IngestionManifestDocument))]
[JsonSerializable(typeof(RetailPricingManifestDocument))]
[JsonSerializable(typeof(RawRowsBlobDocument))]
[JsonSerializable(typeof(RawCatalogueRowsBlobDocument))]
[JsonSerializable(typeof(ResolvedPricesBlobDocument))]
[JsonSerializable(typeof(CataloguePricesBlobDocument))]
[JsonSerializable(typeof(ManualPricesBlobDocument))]
[JsonSerializable(typeof(SubscriptionTruthBlobDocument))]
[JsonSerializable(typeof(RawSubscriptionManagementRow))]
[JsonSerializable(typeof(RawPriceListRow))]
[JsonSerializable(typeof(RawManualPriceEntry))]
[JsonSerializable(typeof(MicrosoftSubscriptionLine))]
[JsonSerializable(typeof(IntendedPrice))]
[JsonSerializable(typeof(PricingResolutionDetail))]
[JsonSerializable(typeof(IngestionLogEntry))]
[JsonSerializable(typeof(SubscriptionManagementCsvIngestionSummary))]
[JsonSerializable(typeof(RetailPricingCsvIngestionSummary))]
[JsonSerializable(typeof(RetailPricingCsvIngestionResult))]
[JsonSerializable(typeof(StripeCatalogueProductsBlobDocument))]
[JsonSerializable(typeof(StripeCataloguePricesBlobDocument))]
[JsonSerializable(typeof(StripeCatalogueProduct))]
[JsonSerializable(typeof(StripeCataloguePrice))]
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

/// <summary>Commit marker for retail pricing ingestion runs.</summary>
public sealed record RetailPricingManifestDocument(
    Guid IngestionId,
    ImportSourceKind SourceKind,
    string? OriginalFileName,
    string ContentFingerprint,
    DateTimeOffset UploadedAt,
    DateTimeOffset CompletedAt,
    string Status,
    RetailPricingCsvIngestionSummary Summary,
    RetailPricingManifestBlobs Blobs,
    RetailPricingManifestContentHashes ContentHashes,
    string? FailureReason = null);

/// <summary>Blob path references for retail pricing manifests.</summary>
public sealed record RetailPricingManifestBlobs(
    string Source,
    string? ManualOverrides,
    string RawCatalogueRows,
    string CataloguePrices,
    string ManualPrices,
    string ResolvedPrices);

/// <summary>Content hashes for retail pricing result blobs.</summary>
public sealed record RetailPricingManifestContentHashes(
    string RawCatalogueRows,
    string ResolvedPrices);

/// <summary>Raw catalogue rows blob payload.</summary>
public sealed record RawCatalogueRowsBlobDocument(
    IReadOnlyList<RawPriceListRow> Records,
    IReadOnlyList<IngestionLogEntry> LogEntries);

/// <summary>Resolved intended prices blob payload.</summary>
public sealed record ResolvedPricesBlobDocument(
    IReadOnlyList<IntendedPrice> Records,
    IReadOnlyList<PricingResolutionDetail> ResolutionDetails,
    IReadOnlyList<IngestionLogEntry> LogEntries);

/// <summary>Catalogue intended prices blob payload.</summary>
public sealed record CataloguePricesBlobDocument(IReadOnlyList<IntendedPrice> Records);

/// <summary>Manual override intended prices blob payload with their originating raw entries.</summary>
public sealed record ManualPricesBlobDocument(
    IReadOnlyList<IntendedPrice> Records,
    IReadOnlyList<RawManualPriceEntry> RawEntries);

/// <summary>Stripe catalogue products blob payload (003 ingestion archive layout).</summary>
public sealed record StripeCatalogueProductsBlobDocument(IReadOnlyList<StripeCatalogueProduct> Records);

/// <summary>Stripe catalogue prices blob payload (003 ingestion archive layout).</summary>
public sealed record StripeCataloguePricesBlobDocument(IReadOnlyList<StripeCataloguePrice> Records);
