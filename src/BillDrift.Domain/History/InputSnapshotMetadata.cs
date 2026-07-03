using BillDrift.Domain.Common;

namespace BillDrift.Domain.History;

/// <summary>Metadata for one input domain snapshot in a reconciliation run archive.</summary>
public sealed record InputSnapshotMetadata(
    InputDomainType Domain,
    bool IsPresent,
    string? SourceFileName = null,
    DateTimeOffset? UploadedAt = null,
    string? ContentFingerprint = null,
    BillingPeriod? BillingPeriodScope = null,
    int RecordCount = 0,
    string? BlobPath = null,
    string? ContentHash = null);

/// <summary>Mapping rules version reference captured at run time.</summary>
public sealed record MappingVersionReference(
    string VersionId,
    string ContentHash,
    DateOnly EffectiveDate,
    string? Label = null);
