using BillDrift.Domain.Classification;

namespace BillDrift.Application.Classification;

/// <summary>
/// Persistence boundary for classification overrides, configuration, and audit history.
/// </summary>
public interface IItemClassificationStore
{
    /// <summary>Loads the persisted resolved classification for a stable key, if any.</summary>
    Task<ItemClassification?> GetClassificationAsync(string stableKey, CancellationToken cancellationToken = default);

    /// <summary>Loads the active override for an item, if any.</summary>
    Task<ClassificationOverride?> GetOverrideAsync(ReconciliationItemRef itemRef, CancellationToken cancellationToken = default);

    /// <summary>Persists an override and appends history.</summary>
    Task SaveOverrideAsync(ClassificationOverride classificationOverride, CancellationToken cancellationToken = default);

    /// <summary>Removes an active override and appends history.</summary>
    Task ClearOverrideAsync(ReconciliationItemRef itemRef, string operatorId, CancellationToken cancellationToken = default);

    /// <summary>Loads operator configuration.</summary>
    Task<ClassificationRuleConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists operator configuration.</summary>
    Task SaveConfigurationAsync(ClassificationRuleConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>Returns recent history entries for an item.</summary>
    Task<IReadOnlyList<ClassificationHistoryEntry>> GetHistoryAsync(
        ReconciliationItemRef itemRef,
        int limit,
        CancellationToken cancellationToken = default);
}
