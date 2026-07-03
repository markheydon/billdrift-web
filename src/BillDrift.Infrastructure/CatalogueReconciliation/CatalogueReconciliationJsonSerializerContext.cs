using System.Text.Json.Serialization;
using BillDrift.Domain.CatalogueReconciliation;

namespace BillDrift.Infrastructure.CatalogueReconciliation;

/// <summary>JSON source generation for catalogue reconciliation blob payloads.</summary>
[JsonSerializable(typeof(CatalogueReconciliationRun))]
[JsonSerializable(typeof(CatalogueReconciliationInputs))]
[JsonSerializable(typeof(CatalogueException))]
[JsonSerializable(typeof(CatalogueProposedFix))]
[JsonSerializable(typeof(CatalogueReconciliationSummary))]
[JsonSerializable(typeof(StripeCatalogueProduct))]
[JsonSerializable(typeof(StripeCataloguePrice))]
[JsonSerializable(typeof(CatalogueRunManifestDocument))]
internal partial class CatalogueReconciliationJsonSerializerContext : JsonSerializerContext;

/// <summary>Manifest describing blob paths for a catalogue reconciliation run.</summary>
internal sealed record CatalogueRunManifestDocument(
    Guid CatalogueRunId,
    DateTimeOffset ArchivedAt,
    string ExceptionsPath,
    string ProposedFixesPath,
    string SummaryPath);
