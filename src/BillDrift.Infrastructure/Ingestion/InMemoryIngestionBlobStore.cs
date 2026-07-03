using System.Collections.Concurrent;
using BillDrift.Application.Import;
using BillDrift.Application.Ingestion;
using BillDrift.Domain.Billing;

namespace BillDrift.Infrastructure.Ingestion;

/// <summary>In-memory blob store for ingestion unit tests.</summary>
public sealed class InMemoryIngestionBlobStore : IIngestionBlobStore
{
    private readonly ConcurrentDictionary<Guid, byte[]> _sources = new();
    private readonly ConcurrentDictionary<Guid, SubscriptionManagementCsvIngestionResult> _results = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<MicrosoftSubscriptionLine>> _subscriptionTruth = new();
    private readonly ConcurrentDictionary<Guid, RetailPricingCsvIngestionResult> _retailResults = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<IntendedPrice>> _resolvedPrices = new();

    /// <inheritdoc />
    public Task<string> UploadSourceAsync(
        Guid ingestionId,
        byte[] content,
        string? originalFileName,
        CancellationToken cancellationToken = default)
    {
        _ = originalFileName;
        _sources[ingestionId] = content;
        return Task.FromResult($"{ingestionId:D}/source/SubscriptionManagementReport.csv");
    }

    /// <inheritdoc />
    public Task<string> PersistResultAsync(
        Guid ingestionId,
        SubscriptionManagementCsvIngestionResult result,
        string? originalFileName,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default)
    {
        _ = originalFileName;
        _ = uploadedAt;
        _results[ingestionId] = result;
        _subscriptionTruth[ingestionId] = result.SubscriptionLines;
        return Task.FromResult($"{ingestionId:D}/result/manifest.json");
    }

    /// <inheritdoc />
    public Task<SubscriptionManagementCsvIngestionResult?> GetIngestionResultAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_results.TryGetValue(ingestionId, out var result) ? result : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<MicrosoftSubscriptionLine>?> GetSubscriptionTruthAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_subscriptionTruth.TryGetValue(ingestionId, out var lines) ? lines : null);

    /// <inheritdoc />
    public Task<string?> UploadManualOverridesAsync(
        Guid ingestionId,
        byte[]? manualOverridesJson,
        CancellationToken cancellationToken = default)
    {
        _ = manualOverridesJson;
        return Task.FromResult<string?>($"{ingestionId:D}/source/manual-overrides.json");
    }

    /// <inheritdoc />
    public Task<string> PersistRetailPricingResultAsync(
        Guid ingestionId,
        RetailPricingCsvIngestionResult result,
        string? originalFileName,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default)
    {
        _ = originalFileName;
        _ = uploadedAt;
        _retailResults[ingestionId] = result;
        _resolvedPrices[ingestionId] = result.ResolvedPrices;
        return Task.FromResult($"{ingestionId:D}/result/manifest.json");
    }

    /// <inheritdoc />
    public Task<RetailPricingCsvIngestionResult?> GetRetailPricingResultAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_retailResults.TryGetValue(ingestionId, out var result) ? result : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<IntendedPrice>?> GetResolvedPricesAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_resolvedPrices.TryGetValue(ingestionId, out var prices) ? prices : null);
}
