namespace BillDrift.Application.Import;

/// <summary>Operator-submitted manual RRP override for a commercial key absent from the catalogue.</summary>
public sealed record ManualPriceOverrideRequest
{
    /// <summary>Offer ID (at least one of offer or SKU required).</summary>
    public string? OfferId { get; init; }

    /// <summary>SKU ID (at least one of offer or SKU required).</summary>
    public string? SkuId { get; init; }

    /// <summary>Contract term as written (e.g. Annual).</summary>
    public required string Term { get; init; }

    /// <summary>Billing frequency as written (e.g. Monthly).</summary>
    public required string Frequency { get; init; }

    /// <summary>Intended retail price (RRP) text.</summary>
    public required string Rrp { get; init; }

    /// <summary>Optional wholesale cost override text.</summary>
    public string? Wholesale { get; init; }

    /// <summary>Operator justification for the override.</summary>
    public required string Reason { get; init; }

    /// <summary>Date from which the override applies.</summary>
    public required DateOnly EffectiveDate { get; init; }
}
