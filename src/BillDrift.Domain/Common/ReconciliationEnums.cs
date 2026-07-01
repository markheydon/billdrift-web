namespace BillDrift.Domain.Common;

/// <summary>
/// Category of drift detected when comparing supplier cost, subscription truth, intended pricing, and Stripe billing.
/// </summary>
public enum MismatchType
{
    /// <summary>A billable item exists in supplier or subscription data but has no corresponding Stripe subscription item.</summary>
    MissingInStripe,

    /// <summary>Licence or subscription quantity differs between domains (e.g. Giacom vs Stripe).</summary>
    QuantityMismatch,

    /// <summary>Monthly vs annual billing frequency differs between intended price and Stripe item.</summary>
    BillingFrequencyMismatch,

    /// <summary>Unit price or total cost differs from the intended price for the same <see cref="CommercialKey"/>.</summary>
    PriceMismatch,

    /// <summary>No Stripe product or price exists in the catalogue for the required commercial key.</summary>
    CatalogueMissing,

    /// <summary>Supplier product name could not be mapped to a Stripe product (no <see cref="Mapping.ProductMapping"/> found).</summary>
    MappingMissing,

    /// <summary>Multiple candidate mappings exist for the same supplier product name.</summary>
    MappingAmbiguous
}

/// <summary>
/// Operator-facing severity of a reconciliation mismatch.
/// </summary>
public enum MismatchSeverity
{
    /// <summary>Informational finding that may not require action.</summary>
    Info,

    /// <summary>Potential drift that should be reviewed.</summary>
    Warning,

    /// <summary>Confirmed billing drift requiring correction in Stripe.</summary>
    Error
}

/// <summary>
/// Corrective action proposed against Stripe to resolve a <see cref="Reconciliation.Mismatch"/>.
/// </summary>
public enum ProposedActionType
{
    /// <summary>Update the quantity on an existing Stripe subscription item.</summary>
    UpdateQuantity,

    /// <summary>Switch a subscription item to a different Stripe price.</summary>
    SwitchPrice,

    /// <summary>Create a new Stripe subscription item for a missing billable product.</summary>
    CreateMissingItem,

    /// <summary>Create or update Stripe catalogue entries (product and prices) before subscription changes.</summary>
    CreateOrUpdateCatalogueEntry
}
