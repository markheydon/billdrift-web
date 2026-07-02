using BillDrift.Application.Classification;
using BillDrift.Domain.Classification;

namespace BillDrift.Application.Tests.Classification;

/// <summary>
/// In-memory store for classification rule and service tests.
/// </summary>
public sealed class InMemoryItemClassificationStore : IItemClassificationStore
{
    private readonly Dictionary<string, ClassificationOverride> _overrides = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ClassificationHistoryEntry>> _history = new(StringComparer.Ordinal);
    private ClassificationRuleConfiguration _configuration = ClassificationRuleConfiguration.Default;

    /// <inheritdoc />
    public Task<ItemClassification?> GetClassificationAsync(
        string stableKey,
        CancellationToken cancellationToken = default)
    {
        if (!_overrides.TryGetValue(stableKey, out var classificationOverride))
        {
            return Task.FromResult<ItemClassification?>(null);
        }

        return Task.FromResult<ItemClassification?>(new ItemClassification(
            classificationOverride.ItemRef,
            classificationOverride.Classification,
            ClassificationSource.ManualOverride,
            "ManualOverride",
            ClassificationConfidence.High,
            classificationOverride.Notes,
            classificationOverride.CreatedAt,
            classificationOverride.OperatorId));
    }

    /// <inheritdoc />
    public Task<ClassificationOverride?> GetOverrideAsync(
        ReconciliationItemRef itemRef,
        CancellationToken cancellationToken = default)
    {
        _overrides.TryGetValue(itemRef.StableKey, out var classificationOverride);
        return Task.FromResult(classificationOverride);
    }

    /// <inheritdoc />
    public Task SaveOverrideAsync(ClassificationOverride classificationOverride, CancellationToken cancellationToken = default)
    {
        _overrides.TryGetValue(classificationOverride.ItemRef.StableKey, out var prior);
        _overrides[classificationOverride.ItemRef.StableKey] = classificationOverride;

        AppendHistory(new ClassificationHistoryEntry(
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
    public Task ClearOverrideAsync(
        ReconciliationItemRef itemRef,
        string operatorId,
        CancellationToken cancellationToken = default)
    {
        if (_overrides.Remove(itemRef.StableKey, out var prior))
        {
            AppendHistory(new ClassificationHistoryEntry(
                itemRef,
                prior.Classification,
                prior.Classification,
                ClassificationSource.Automatic,
                "Override cleared",
                operatorId,
                DateTimeOffset.UtcNow));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ClassificationRuleConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_configuration);

    /// <inheritdoc />
    public Task SaveConfigurationAsync(
        ClassificationRuleConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        _configuration = configuration;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ClassificationHistoryEntry>> GetHistoryAsync(
        ReconciliationItemRef itemRef,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!_history.TryGetValue(itemRef.StableKey, out var entries))
        {
            return Task.FromResult<IReadOnlyList<ClassificationHistoryEntry>>([]);
        }

        return Task.FromResult<IReadOnlyList<ClassificationHistoryEntry>>(
            entries.OrderByDescending(entry => entry.Timestamp).Take(limit).ToList());
    }

    /// <summary>Sets configuration for test setup.</summary>
    public void SetConfiguration(ClassificationRuleConfiguration configuration) => _configuration = configuration;

    private void AppendHistory(ClassificationHistoryEntry entry)
    {
        if (!_history.TryGetValue(entry.ItemRef.StableKey, out var entries))
        {
            entries = [];
            _history[entry.ItemRef.StableKey] = entries;
        }

        entries.Add(entry);
    }
}
