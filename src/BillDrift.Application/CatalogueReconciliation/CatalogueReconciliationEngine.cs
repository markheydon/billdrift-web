using BillDrift.Application.CatalogueReconciliation.Stages;
using BillDrift.Application.Mapping;
using BillDrift.Application.Reconciliation.Matching;
using BillDrift.Domain.CatalogueReconciliation;

namespace BillDrift.Application.CatalogueReconciliation;

/// <summary>Deterministic catalogue reconciliation engine.</summary>
public interface ICatalogueReconciliationEngine
{
    /// <summary>Executes catalogue reconciliation over the provided input snapshot.</summary>
    CatalogueReconciliationRun Execute(
        CatalogueReconciliationInputs inputs,
        CatalogueReconciliationOptions? options = null,
        CatalogueRunId? runId = null);
}

/// <summary>Default implementation of <see cref="ICatalogueReconciliationEngine"/>.</summary>
public sealed class CatalogueReconciliationEngine : ICatalogueReconciliationEngine
{
    private readonly IReadOnlyList<ICatalogueReconciliationStage> _stages;

    /// <summary>Creates the engine with default pipeline stages.</summary>
    public CatalogueReconciliationEngine(IProductMappingResolver mappingResolver)
        : this(BuildDefaultStages(mappingResolver))
    {
    }

    /// <summary>Creates the engine with explicit stages (for testing).</summary>
    public CatalogueReconciliationEngine(IReadOnlyList<ICatalogueReconciliationStage> stages)
    {
        _stages = stages;
    }

    /// <inheritdoc />
    public CatalogueReconciliationRun Execute(
        CatalogueReconciliationInputs inputs,
        CatalogueReconciliationOptions? options = null,
        CatalogueRunId? runId = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var effectiveOptions = options ?? new CatalogueReconciliationOptions();
        var effectiveRunId = runId ?? CatalogueRunId.New();
        var context = new CatalogueReconciliationContext(effectiveRunId, inputs, effectiveOptions);

        foreach (var stage in _stages)
        {
            stage.Execute(context);

            // Fail fast on invalid input so callers never receive a success-style run and no
            // invalid run is ever persisted. ValidateInputsStage runs first, so this short-circuits
            // before indexing (which would otherwise NRE on missing mappings/prices).
            if (context.ValidationError is not null)
            {
                throw new CatalogueReconciliationValidationException(context.ValidationError);
            }
        }

        var summary = BuildSummary(context);
        return new CatalogueReconciliationRun(
            effectiveRunId,
            DateTimeOffset.UtcNow,
            inputs,
            context.Exceptions,
            context.ProposedFixes,
            summary,
            effectiveOptions);
    }

    private static CatalogueReconciliationSummary BuildSummary(CatalogueReconciliationContext context)
    {
        var byType = context.Exceptions
            .GroupBy(e => e.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        var unmappedProducts = context.Exceptions.Count(e => e.Type == CatalogueExceptionType.UnmappedCatalogueEntry);

        return new CatalogueReconciliationSummary(
            context.MappedProductsChecked,
            byType,
            context.ProposedFixes.Count(f => f.IsActionable),
            context.ProposedFixes.Count(f => !f.IsActionable),
            unmappedProducts,
            0);
    }

    private static IReadOnlyList<ICatalogueReconciliationStage> BuildDefaultStages(IProductMappingResolver mappingResolver)
    {
        var fuzzyMatcher = new DeterministicFuzzyNameMatcher();
        return
        [
            new ValidateInputsStage(),
            new BuildIndexesStage(mappingResolver, fuzzyMatcher),
            new DetectDuplicateConflictsStage(),
            new DetectUnmappedCatalogueStage(),
            new ReconcileMappedProductsStage(),
            new AttachProposedFixesStage(),
            new OrderOutputStage()
        ];
    }
}
