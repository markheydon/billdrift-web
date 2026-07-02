using BillDrift.Application.Mapping;
using BillDrift.Application.Reconciliation.Indexing;
using BillDrift.Application.Reconciliation.Matching;

namespace BillDrift.Application.Reconciliation.Stages;

/// <summary>
/// Builds all lookup indexes from reconciliation inputs.
/// </summary>
public sealed class IndexBuildStage : IReconciliationStage
{
    private readonly IProductMappingResolver _mappingResolver;
    private readonly DeterministicFuzzyNameMatcher _fuzzyMatcher;

    /// <summary>
    /// Creates an index build stage with mapping resolver and fuzzy matcher dependencies.
    /// </summary>
    public IndexBuildStage(IProductMappingResolver mappingResolver, DeterministicFuzzyNameMatcher fuzzyMatcher)
    {
        _mappingResolver = mappingResolver;
        _fuzzyMatcher = fuzzyMatcher;
    }

    /// <inheritdoc />
    public void Execute(ReconciliationContext context)
    {
        var inputs = context.Request.Inputs;

        context.IntendedPriceIndex = IntendedPriceIndex.Build(
            inputs.IntendedPrices ?? []);

        context.StripeCatalogueIndex = StripeCatalogueIndex.Build(
            inputs.StripeItems ?? []);

        context.ProductMappingIndex = ProductMappingIndex.Build(
            inputs.ProductMappings ?? [],
            _mappingResolver,
            _fuzzyMatcher);
    }
}
