namespace BillDrift.Domain.Common;

/// <summary>
/// Commerce platform classification for Giacom price list entries (NCE vs Legacy CSP).
/// </summary>
public enum PricingPlatform
{
    /// <summary>New Commerce Experience platform pricing.</summary>
    Nce,

    /// <summary>Legacy CSP platform pricing.</summary>
    Legacy,

    /// <summary>Platform not specified or unrecognised in the source export.</summary>
    Unknown
}
