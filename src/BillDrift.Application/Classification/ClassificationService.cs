using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Classification;

/// <summary>
/// Orchestrates classification pipeline: load config/overrides, build signals, apply rules.
/// </summary>
public sealed class ClassificationService
{
    private readonly IItemClassificationStore _store;
    private readonly ClassificationRuleEngine _ruleEngine;

    /// <summary>
    /// Creates a classification service with store and rule engine dependencies.
    /// </summary>
    public ClassificationService(IItemClassificationStore store, ClassificationRuleEngine ruleEngine)
    {
        _store = store;
        _ruleEngine = ruleEngine;
    }

    /// <summary>
    /// Classifies all in-scope items from reconciliation inputs.
    /// </summary>
    public async Task<ClassificationContext> ClassifyAsync(
        ReconciliationInputs inputs,
        BillingPeriod scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var config = await _store.GetConfigurationAsync(cancellationToken);
        var itemRefs = ReconciliationItemRefFactory.ExtractAll(inputs, scope);
        var classifiedAt = DateTimeOffset.UtcNow;
        var results = new Dictionary<string, ItemClassification>(StringComparer.Ordinal);

        foreach (var itemRef in itemRefs)
        {
            var activeOverride = await _store.GetOverrideAsync(itemRef, cancellationToken);
            var signals = ClassificationSignalBuilder.Build(itemRef, inputs, scope, config);
            var classification = _ruleEngine.Evaluate(signals, config, activeOverride, classifiedAt);
            results[itemRef.StableKey] = classification;
        }

        return new ClassificationContext(results, classifiedAt);
    }

    /// <summary>
    /// Loads the persisted resolved classification for a stable key, if one exists.
    /// Returns <c>null</c> when no classification has been persisted for the key (automatic
    /// classifications are computed during reconciliation runs against live inputs).
    /// </summary>
    public Task<ItemClassification?> GetClassificationAsync(
        string stableKey,
        CancellationToken cancellationToken = default) =>
        _store.GetClassificationAsync(stableKey, cancellationToken);

    /// <summary>
    /// Applies a manual classification override with validation.
    /// </summary>
    public async Task<ItemClassification> ApplyOverrideAsync(
        ClassificationOverride classificationOverride,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(classificationOverride);

        var config = await _store.GetConfigurationAsync(cancellationToken);
        ValidateOverrideNotes(classificationOverride, config);

        await _store.SaveOverrideAsync(classificationOverride, cancellationToken);

        return new ItemClassification(
            classificationOverride.ItemRef,
            classificationOverride.Classification,
            ClassificationSource.ManualOverride,
            "ManualOverride",
            ClassificationConfidence.High,
            classificationOverride.Notes,
            classificationOverride.CreatedAt,
            classificationOverride.OperatorId);
    }

    /// <summary>
    /// Clears a manual override without re-evaluating classification. Use this when no live
    /// reconciliation context is available (for example from an HTTP endpoint); the automatic
    /// classification is recomputed on the next reconciliation run against live inputs.
    /// </summary>
    public Task ClearOverrideAsync(
        ReconciliationItemRef itemRef,
        string operatorId,
        CancellationToken cancellationToken = default) =>
        _store.ClearOverrideAsync(itemRef, operatorId, cancellationToken);

    /// <summary>
    /// Clears a manual override and returns the automatic re-evaluation result computed against
    /// the supplied live reconciliation inputs and scope.
    /// </summary>
    public async Task<ItemClassification> ClearOverrideAsync(
        ReconciliationItemRef itemRef,
        string operatorId,
        ReconciliationInputs inputs,
        BillingPeriod scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        await _store.ClearOverrideAsync(itemRef, operatorId, cancellationToken);

        var config = await _store.GetConfigurationAsync(cancellationToken);
        var signals = ClassificationSignalBuilder.Build(itemRef, inputs, scope, config);
        var classifiedAt = DateTimeOffset.UtcNow;
        return _ruleEngine.Evaluate(signals, config, activeOverride: null, classifiedAt);
    }

    /// <summary>
    /// Loads operator configuration from persistence.
    /// </summary>
    public Task<ClassificationRuleConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default) =>
        _store.GetConfigurationAsync(cancellationToken);

    /// <summary>
    /// Persists operator configuration.
    /// </summary>
    public Task UpdateConfigurationAsync(
        ClassificationRuleConfiguration configuration,
        CancellationToken cancellationToken = default) =>
        _store.SaveConfigurationAsync(configuration, cancellationToken);

    /// <summary>
    /// Returns audit history for an item.
    /// </summary>
    public Task<IReadOnlyList<ClassificationHistoryEntry>> GetHistoryAsync(
        ReconciliationItemRef itemRef,
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        _store.GetHistoryAsync(itemRef, limit, cancellationToken);

    private static void ValidateOverrideNotes(ClassificationOverride classificationOverride, ClassificationRuleConfiguration config)
    {
        if (!config.RequireNotesForAlertSuppression)
        {
            return;
        }

        if (classificationOverride.Classification is ReconciliationItemClassification.Internal
            or ReconciliationItemClassification.CustomService &&
            string.IsNullOrWhiteSpace(classificationOverride.Notes))
        {
            throw new DomainValidationException(
                nameof(classificationOverride.Notes),
                "Notes are required when overriding to Internal or CustomService.");
        }
    }
}
