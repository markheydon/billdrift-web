namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>Category of catalogue drift detected during Stripe catalogue reconciliation.</summary>
public enum CatalogueExceptionType
{
    MissingProduct,
    MissingPrice,
    IncorrectPrice,
    DuplicateProduct,
    DuplicatePrice,
    PricingReferenceGap,
    MappingAmbiguous,
    UnmappedCatalogueEntry
}
