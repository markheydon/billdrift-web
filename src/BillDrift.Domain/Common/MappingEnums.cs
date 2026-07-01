namespace BillDrift.Domain.Common;

/// <summary>
/// Whether a mapped product is a Cloud Solution Provider (CSP) offering or a non-CSP product.
/// </summary>
public enum ProductClassification
{
    /// <summary>Cloud Solution Provider product requiring CSP-specific handling.</summary>
    Csp,

    /// <summary>Non-CSP product.</summary>
    NonCsp
}

/// <summary>
/// Confidence level of a <see cref="Mapping.ProductMapping"/> between supplier product names and Stripe IDs.
/// Low confidence mappings may produce <see cref="MismatchType.MappingMissing"/> or <see cref="MismatchType.MappingAmbiguous"/> mismatches.
/// </summary>
public enum MappingConfidence
{
    /// <summary>Mapping is reliable and suitable for automatic reconciliation.</summary>
    High,

    /// <summary>Mapping is probably correct but may need operator review.</summary>
    Medium,

    /// <summary>Mapping is uncertain and should be reviewed before acting on proposed changes.</summary>
    Low,

    /// <summary>No mapping exists for this product.</summary>
    Unmapped
}

/// <summary>
/// How a <see cref="Mapping.ProductMapping"/> was established.
/// </summary>
public enum MappingSource
{
    /// <summary>Mapping was created or confirmed by an operator.</summary>
    Manual,

    /// <summary>Mapping was imported from an external source.</summary>
    Imported,

    /// <summary>Mapping was inferred automatically from supplier and Stripe data.</summary>
    Inferred
}

/// <summary>
/// Confidence that entities in an <see cref="Reconciliation.EntityMatchGroup"/> truly represent the same commercial product.
/// </summary>
public enum MatchConfidence
{
    /// <summary>Strong evidence links all present entities in the match group.</summary>
    High,

    /// <summary>Entities are likely matched but some correlation signals are weak.</summary>
    Medium,

    /// <summary>Match is tentative and may be incorrect.</summary>
    Low,

    /// <summary>No meaningful match could be established across domains.</summary>
    None
}
