using BillDrift.Application.Mapping;
using BillDrift.Application.Reconciliation.Detection;
using BillDrift.Application.Reconciliation.Matching;
using BillDrift.Application.Reconciliation.Stages;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation;

/// <summary>
/// Orchestrates ordered reconciliation pipeline stages over a shared context.
/// </summary>
public sealed class ReconciliationPipeline
{
    private readonly IProductMappingResolver _mappingResolver;
    private readonly DeterministicFuzzyNameMatcher _fuzzyMatcher;
    private readonly ProposedChangeFactory _proposedChangeFactory;
    private readonly MismatchDetector _mismatchDetector;

    /// <summary>
    /// Creates a pipeline with mapping resolver dependency.
    /// </summary>
    public ReconciliationPipeline(IProductMappingResolver mappingResolver)
    {
        _mappingResolver = mappingResolver;
        _fuzzyMatcher = new DeterministicFuzzyNameMatcher();
        _proposedChangeFactory = new ProposedChangeFactory();
        _mismatchDetector = new MismatchDetector(_proposedChangeFactory);
    }

    /// <summary>
    /// Executes all pipeline stages and returns the completed reconciliation run.
    /// </summary>
    /// <param name="request">Reconciliation request with scope, inputs, and options.</param>
    /// <returns>Completed reconciliation run with match groups, mismatches, and proposed changes.</returns>
    public ReconciliationRun Execute(ReconciliationRequest request)
    {
        var context = new ReconciliationContext(request);

        try
        {
            new InputValidationStage().Execute(context);

            new IndexBuildStage(_mappingResolver, _fuzzyMatcher).Execute(context);

            var keyResolver = new CommercialKeyResolver(context.ProductMappingIndex, _fuzzyMatcher);
            new MatchGroupBuildStage(keyResolver, new StripeItemMatcher(), new CustomerMatcher())
                .Execute(context);

            new SubscriptionTruthReconcileStage(_mismatchDetector).Execute(context);
            new SupplierCostReconcileStage(_mismatchDetector).Execute(context);
            new CatalogueReconcileStage(_mismatchDetector).Execute(context);
            new OutputOrderingStage().Execute(context);
        }
        catch (DomainValidationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not ReconciliationException)
        {
            throw new ReconciliationException($"Pipeline failed: {ex.Message}");
        }

        return new ReconciliationRun(
            context.RunId,
            DateTimeOffset.UtcNow,
            request.Scope,
            request.Inputs,
            context.MatchGroups.AsReadOnly(),
            context.Mismatches.AsReadOnly(),
            context.ProposedChanges.AsReadOnly());
    }
}
