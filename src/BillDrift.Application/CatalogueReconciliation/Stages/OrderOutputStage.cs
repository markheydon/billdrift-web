using BillDrift.Domain.CatalogueReconciliation;

namespace BillDrift.Application.CatalogueReconciliation.Stages;

/// <summary>Orders exceptions and proposed fixes deterministically.</summary>
public sealed class OrderOutputStage : ICatalogueReconciliationStage
{
    private static readonly CatalogueExceptionType[] TypeOrder =
    [
        CatalogueExceptionType.DuplicateProduct,
        CatalogueExceptionType.DuplicatePrice,
        CatalogueExceptionType.MappingAmbiguous,
        CatalogueExceptionType.PricingReferenceGap,
        CatalogueExceptionType.MissingProduct,
        CatalogueExceptionType.MissingPrice,
        CatalogueExceptionType.IncorrectPrice,
        CatalogueExceptionType.UnmappedCatalogueEntry
    ];

    /// <inheritdoc />
    public void Execute(CatalogueReconciliationContext context)
    {
        if (context.ValidationError is not null)
        {
            return;
        }

        context.Exceptions.Sort(CompareExceptions);
        context.ProposedFixes.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
    }

    private static int CompareExceptions(CatalogueException a, CatalogueException b)
    {
        var typeCompare = Array.IndexOf(TypeOrder, a.Type).CompareTo(Array.IndexOf(TypeOrder, b.Type));
        if (typeCompare != 0)
        {
            return typeCompare;
        }

        var offerA = a.CommercialKeyRoot?.OfferId.Value ?? a.CommercialKey?.OfferId.Value ?? string.Empty;
        var offerB = b.CommercialKeyRoot?.OfferId.Value ?? b.CommercialKey?.OfferId.Value ?? string.Empty;
        var offerCompare = string.Compare(offerA, offerB, StringComparison.Ordinal);
        if (offerCompare != 0)
        {
            return offerCompare;
        }

        var skuA = a.CommercialKeyRoot?.SkuId.Value ?? a.CommercialKey?.SkuId.Value ?? string.Empty;
        var skuB = b.CommercialKeyRoot?.SkuId.Value ?? b.CommercialKey?.SkuId.Value ?? string.Empty;
        var skuCompare = string.Compare(skuA, skuB, StringComparison.Ordinal);
        if (skuCompare != 0)
        {
            return skuCompare;
        }

        return a.Id.Value.CompareTo(b.Id.Value);
    }
}
