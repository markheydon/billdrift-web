using System.Collections.Concurrent;
using BillDrift.Application.Classification;
using BillDrift.Domain.Classification;

namespace BillDrift.Infrastructure.Classification;

/// <summary>In-memory classification store for integration tests.</summary>
public sealed class InMemoryItemClassificationStore : IItemClassificationStore
{
    private readonly ConcurrentDictionary<string, ItemClassification> _classifications = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ClassificationOverride> _overrides = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<ClassificationHistoryEntry> _history = [];
    private ClassificationRuleConfiguration _configuration = ClassificationRuleConfiguration.Default;

    /// <inheritdoc />
    public Task<ItemClassification?> GetClassificationAsync(string stableKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_classifications.TryGetValue(stableKey, out var item) ? item : null);

    /// <inheritdoc />
    public Task<ClassificationOverride?> GetOverrideAsync(ReconciliationItemRef itemRef, CancellationToken cancellationToken = default) =>
        Task.FromResult(_overrides.TryGetValue(OverrideKey(itemRef), out var item) ? item : null);

    /// <inheritdoc />
    public Task SaveOverrideAsync(ClassificationOverride classificationOverride, CancellationToken cancellationToken = default)
    {
        var prior = _overrides.TryGetValue(OverrideKey(classificationOverride.ItemRef), out var existing) ? existing : null;
        _overrides[OverrideKey(classificationOverride.ItemRef)] = classificationOverride;
        _classifications[classificationOverride.ItemRef.StableKey] = new ItemClassification(
            classificationOverride.ItemRef,
            classificationOverride.Classification,
            ClassificationSource.ManualOverride,
            "ManualOverride",
            ClassificationConfidence.High,
            classificationOverride.Notes,
            classificationOverride.CreatedAt,
            classificationOverride.OperatorId);

        _history.Add(new ClassificationHistoryEntry(
            classificationOverride.ItemRef,
            prior?.Classification,
            classificationOverride.Classification,
            ClassificationSource.ManualOverride,
            classificationOverride.Notes,
            classificationOverride.OperatorId,
            classificationOverride.CreatedAt));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearOverrideAsync(ReconciliationItemRef itemRef, string operatorId, CancellationToken cancellationToken = default)
    {
        if (!_overrides.TryRemove(OverrideKey(itemRef), out var prior))
        {
            return Task.CompletedTask;
        }

        _classifications.TryRemove(itemRef.StableKey, out _);
        _history.Add(new ClassificationHistoryEntry(
            itemRef,
            prior.Classification,
            prior.Classification,
            ClassificationSource.Automatic,
            "Override cleared",
            operatorId,
            DateTimeOffset.UtcNow));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ClassificationRuleConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_configuration);

    /// <inheritdoc />
    public Task SaveConfigurationAsync(ClassificationRuleConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _configuration = configuration;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ClassificationHistoryEntry>> GetHistoryAsync(
        ReconciliationItemRef itemRef,
        int limit,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ClassificationHistoryEntry>>(
            _history
                .Where(h => h.ItemRef.StableKey == itemRef.StableKey)
                .OrderByDescending(h => h.Timestamp)
                .Take(limit)
                .ToList());

    private static string OverrideKey(ReconciliationItemRef itemRef) =>
        $"{itemRef.Kind}:{itemRef.StableKey}:{itemRef.CustomerMexId.Value}";
}
