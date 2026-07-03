namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>References to upstream ingestion snapshots used by a catalogue reconciliation run.</summary>
public sealed record CatalogueInputReferences(
    Guid? StripeIngestionRunId,
    Guid? PricingIngestionRunId,
    string? MappingVersionId,
    string? MappingContentHash);
