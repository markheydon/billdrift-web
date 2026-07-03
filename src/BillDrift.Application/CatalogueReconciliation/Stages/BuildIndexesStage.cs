using BillDrift.Application.Mapping;
using BillDrift.Application.Reconciliation.Indexing;
using BillDrift.Application.Reconciliation.Matching;

namespace BillDrift.Application.CatalogueReconciliation.Stages;

/// <summary>Builds in-memory indexes from input snapshots.</summary>
public sealed class BuildIndexesStage : ICatalogueReconciliationStage
{
    private readonly IProductMappingResolver _mappingResolver;
    private readonly DeterministicFuzzyNameMatcher _fuzzyMatcher;

    /// <summary>Creates the index build stage.</summary>
    public BuildIndexesStage(
        IProductMappingResolver mappingResolver,
        DeterministicFuzzyNameMatcher fuzzyMatcher)
    {
        _mappingResolver = mappingResolver;
        _fuzzyMatcher = fuzzyMatcher;
    }

    /// <inheritdoc />
    public void Execute(CatalogueReconciliationContext context)
    {
        if (context.ValidationError is not null)
        {
            return;
        }

        context.CatalogueIndex = StripeCatalogueSnapshotIndex.Build(
            context.Inputs.StripeProducts,
            context.Inputs.StripePrices);

        context.ProductMappingIndex = ProductMappingIndex.Build(
            context.Inputs.ProductMappings,
            _mappingResolver,
            _fuzzyMatcher);

        context.IntendedPriceIndex = IntendedPriceIndex.Build(context.Inputs.IntendedPrices);
    }
}
