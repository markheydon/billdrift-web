using BillDrift.Application.CatalogueReconciliation.Detection;
using BillDrift.Domain.Common;

namespace BillDrift.Application.CatalogueReconciliation.Stages;

/// <summary>Flags Stripe catalogue entries with no canonical mapping or metadata.</summary>
public sealed class DetectUnmappedCatalogueStage : ICatalogueReconciliationStage
{
    private readonly CatalogueExceptionFactory _factory = new();

    /// <inheritdoc />
    public void Execute(CatalogueReconciliationContext context)
    {
        if (context.ValidationError is not null || context.CatalogueIndex is null || context.ProductMappingIndex is null)
        {
            return;
        }

        var mappedProductIds = new HashSet<StripeProductId>(
            context.Inputs.ProductMappings.Select(m => m.StripeProductId));

        foreach (var product in context.CatalogueIndex.AllProducts)
        {
            if (product.OfferId is not null && product.SkuId is not null)
            {
                continue;
            }

            if (mappedProductIds.Contains(product.ProductId))
            {
                continue;
            }

            context.Exceptions.Add(_factory.UnmappedProduct(product));
        }
    }
}
