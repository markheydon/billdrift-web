using BillDrift.Domain.CatalogueReconciliation;

namespace BillDrift.Application.CatalogueReconciliation;

/// <summary>Persists catalogue reconciliation runs to Azure Blob and Table storage.</summary>
public interface ICatalogueReconciliationStore
{
    /// <summary>Archives a completed catalogue reconciliation run.</summary>
    Task SaveRunAsync(CatalogueReconciliationRun run, CancellationToken cancellationToken = default);

    /// <summary>Loads a catalogue reconciliation run by identifier.</summary>
    Task<CatalogueReconciliationRun?> GetRunAsync(CatalogueRunId runId, CancellationToken cancellationToken = default);

    /// <summary>Lists recent catalogue reconciliation runs.</summary>
    Task<IReadOnlyList<CatalogueRunListItem>> ListRunsAsync(int limit, CancellationToken cancellationToken = default);
}

/// <summary>Summary row for catalogue run list queries.</summary>
public sealed record CatalogueRunListItem(
    CatalogueRunId CatalogueRunId,
    DateTimeOffset ExecutedAt,
    int TotalExceptions,
    int MissingProductCount,
    int MissingPriceCount,
    int IncorrectPriceCount,
    int DuplicateCount,
    int ActionableFixCount,
    Guid? StripeIngestionRunId,
    Guid? PricingIngestionRunId);
