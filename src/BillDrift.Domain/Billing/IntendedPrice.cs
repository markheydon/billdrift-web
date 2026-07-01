using BillDrift.Domain.Common;

namespace BillDrift.Domain.Billing;

public sealed record IntendedPrice(
    IntendedPriceId Id,
    CommercialKey Key,
    Money Wholesale,
    Money Rrp,
    Money? Margin,
    decimal? MarginPercent,
    PriceListStatus Status,
    PriceSource Source,
    SourceReference SourceReference);
