namespace BillDrift.Domain.Common;

/// <summary>
/// Contract term length as normalized from Giacom or Stripe source strings.
/// Application-layer mapping rules translate raw values (e.g. "P1M") into these domain values.
/// </summary>
public enum Term
{
    /// <summary>Monthly contract term.</summary>
    Monthly,

    /// <summary>Annual contract term.</summary>
    Annual,

    /// <summary>Three-year contract term.</summary>
    Triennial,

    /// <summary>ISO 8601 one-month period identifier from source systems.</summary>
    P1M,

    /// <summary>ISO 8601 one-year period identifier from source systems.</summary>
    P1Y,

    /// <summary>Term could not be determined from the source data.</summary>
    Unknown
}

/// <summary>
/// How often a subscription or price is billed (monthly vs annual).
/// Used with <see cref="Term"/> in <see cref="CommercialKey"/> for price alignment.
/// </summary>
public enum BillingFrequency
{
    /// <summary>Customer is billed every month.</summary>
    Monthly,

    /// <summary>Customer is billed every year.</summary>
    Annual,

    /// <summary>Billing frequency could not be determined from the source data.</summary>
    Unknown
}
