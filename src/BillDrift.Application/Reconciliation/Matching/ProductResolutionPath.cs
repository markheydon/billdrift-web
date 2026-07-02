namespace BillDrift.Application.Reconciliation.Matching;

/// <summary>
/// Audit trail for how a product identity was resolved during reconciliation matching.
/// </summary>
public enum ProductResolutionPath
{
    /// <summary>Offer and SKU present on the source line.</summary>
    ExplicitOfferSku,

    /// <summary>Offer and SKU extracted from Stripe product metadata.</summary>
    StripeMetadata,

    /// <summary>Product mapping lookup by commercial key root.</summary>
    MappingByRoot,

    /// <summary>Exact supplier name variant match via product mapping resolver.</summary>
    NameVariantExact,

    /// <summary>Deterministic fuzzy name fallback match.</summary>
    NameFuzzy,

    /// <summary>No product identity could be resolved.</summary>
    Unresolved
}
