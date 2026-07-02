namespace BillDrift.Domain.Classification;

/// <summary>
/// Which normalized entity kind a reconciliation item reference represents.
/// </summary>
public enum ReconciliationItemKind
{
    SupplierCost,
    SubscriptionTruth,
    StripeBilling
}

/// <summary>
/// Product family inferred from category rules for classification.
/// </summary>
public enum ProductCategory
{
    Microsoft365,
    Other,
    CustomService
}

/// <summary>
/// How a product category rule matches against item metadata.
/// </summary>
public enum ProductCategoryMatchKind
{
    OfferIdPrefix,
    SkuIdPrefix,
    ProductNameContains
}
