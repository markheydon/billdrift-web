using BillDrift.Application.Reconciliation;

namespace BillDrift.Application.Classification.Stages;

/// <summary>
/// Pre-pipeline stage that classifies all reconciliation items when classifications are not pre-supplied.
/// </summary>
public sealed class ClassificationEnrichmentStage
{
    private readonly ClassificationService _classificationService;

    /// <summary>
    /// Creates an enrichment stage with classification service dependency.
    /// </summary>
    public ClassificationEnrichmentStage(ClassificationService classificationService)
    {
        _classificationService = classificationService;
    }

    /// <summary>
    /// Classifies items and assigns result to context when not already provided on the request.
    /// </summary>
    public async Task ExecuteAsync(ReconciliationContext context, CancellationToken cancellationToken = default)
    {
        if (context.Classifications is not null || context.Request.Classifications is not null)
        {
            context.Classifications ??= context.Request.Classifications;
            return;
        }

        context.Classifications = await _classificationService.ClassifyAsync(
            context.Request.Inputs,
            context.Request.Scope,
            cancellationToken);
    }
}
