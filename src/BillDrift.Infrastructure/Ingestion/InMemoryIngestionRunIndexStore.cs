using System.Collections.Concurrent;
using BillDrift.Application.Ingestion;

namespace BillDrift.Infrastructure.Ingestion;

/// <summary>In-memory table index store for ingestion unit tests.</summary>
public sealed class InMemoryIngestionRunIndexStore : IIngestionRunIndexStore
{
    private readonly ConcurrentDictionary<Guid, SubscriptionManagementIngestionRun> _runs = new();
    private readonly ConcurrentDictionary<Guid, RetailPricingIngestionRun> _retailRuns = new();

    /// <inheritdoc />
    public Task CreateInProgressAsync(SubscriptionManagementIngestionRun run, CancellationToken cancellationToken = default)
    {
        _runs[run.IngestionId] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CompleteAsync(SubscriptionManagementIngestionRun run, CancellationToken cancellationToken = default)
    {
        _runs[run.IngestionId] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task FailAsync(SubscriptionManagementIngestionRun run, CancellationToken cancellationToken = default)
    {
        _runs[run.IngestionId] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SubscriptionManagementIngestionRun?> GetByIdAsync(Guid ingestionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_runs.TryGetValue(ingestionId, out var run) ? run : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<SubscriptionManagementIngestionRun>> ListRecentAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var items = _runs.Values
            .OrderByDescending(r => r.UploadedAt)
            .Take(take)
            .ToList();
        return Task.FromResult<IReadOnlyList<SubscriptionManagementIngestionRun>>(items);
    }

    /// <inheritdoc />
    public Task CreateRetailPricingInProgressAsync(RetailPricingIngestionRun run, CancellationToken cancellationToken = default)
    {
        _retailRuns[run.IngestionId] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CompleteRetailPricingAsync(RetailPricingIngestionRun run, CancellationToken cancellationToken = default)
    {
        _retailRuns[run.IngestionId] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task FailRetailPricingAsync(RetailPricingIngestionRun run, CancellationToken cancellationToken = default)
    {
        _retailRuns[run.IngestionId] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<RetailPricingIngestionRun?> GetRetailPricingByIdAsync(Guid ingestionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_retailRuns.TryGetValue(ingestionId, out var run) ? run : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<RetailPricingIngestionRun>> ListRecentRetailPricingAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var items = _retailRuns.Values
            .OrderByDescending(r => r.UploadedAt)
            .Take(take)
            .ToList();
        return Task.FromResult<IReadOnlyList<RetailPricingIngestionRun>>(items);
    }
}
