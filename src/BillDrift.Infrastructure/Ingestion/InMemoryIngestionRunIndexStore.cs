using System.Collections.Concurrent;
using BillDrift.Application.Ingestion;

namespace BillDrift.Infrastructure.Ingestion;

/// <summary>In-memory table index store for ingestion unit tests.</summary>
public sealed class InMemoryIngestionRunIndexStore : IIngestionRunIndexStore
{
    private readonly ConcurrentDictionary<Guid, SubscriptionManagementIngestionRun> _runs = new();
    private readonly ConcurrentDictionary<Guid, RetailPricingIngestionRun> _retailRuns = new();
    private readonly ConcurrentDictionary<Guid, GiacomPdfIngestionRun> _giacomPdfRuns = new();
    private readonly ConcurrentDictionary<Guid, StripeCsvIngestionRun> _stripeCsvRuns = new();

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

    /// <inheritdoc />
    public Task CreateGiacomPdfInProgressAsync(GiacomPdfIngestionRun run, CancellationToken cancellationToken = default)
    {
        _giacomPdfRuns[run.IngestionId] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CompleteGiacomPdfAsync(GiacomPdfIngestionRun run, CancellationToken cancellationToken = default)
    {
        _giacomPdfRuns[run.IngestionId] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task FailGiacomPdfAsync(GiacomPdfIngestionRun run, CancellationToken cancellationToken = default)
    {
        _giacomPdfRuns[run.IngestionId] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<GiacomPdfIngestionRun?> GetGiacomPdfByIdAsync(Guid ingestionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_giacomPdfRuns.TryGetValue(ingestionId, out var run) ? run : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<GiacomPdfIngestionRun>> ListRecentGiacomPdfAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var items = _giacomPdfRuns.Values
            .OrderByDescending(r => r.UploadedAt)
            .Take(take)
            .ToList();
        return Task.FromResult<IReadOnlyList<GiacomPdfIngestionRun>>(items);
    }

    /// <inheritdoc />
    public Task CreateStripeCsvInProgressAsync(StripeCsvIngestionRun run, CancellationToken cancellationToken = default)
    {
        _stripeCsvRuns[run.IngestionId] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CompleteStripeCsvAsync(StripeCsvIngestionRun run, CancellationToken cancellationToken = default)
    {
        _stripeCsvRuns[run.IngestionId] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task FailStripeCsvAsync(StripeCsvIngestionRun run, CancellationToken cancellationToken = default)
    {
        _stripeCsvRuns[run.IngestionId] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<StripeCsvIngestionRun?> GetStripeCsvByIdAsync(Guid ingestionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_stripeCsvRuns.TryGetValue(ingestionId, out var run) ? run : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<StripeCsvIngestionRun>> ListRecentStripeCsvAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var items = _stripeCsvRuns.Values
            .OrderByDescending(r => r.UploadedAt)
            .Take(take)
            .ToList();
        return Task.FromResult<IReadOnlyList<StripeCsvIngestionRun>>(items);
    }
}
