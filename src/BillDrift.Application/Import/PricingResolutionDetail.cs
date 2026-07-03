using BillDrift.Domain.Common;

namespace BillDrift.Application.Import;

/// <summary>Per-key outcome of pricing strategy resolution for one ingestion run.</summary>
public sealed record PricingResolutionDetail(
    CommercialKey CommercialKey,
    PriceSource WinningSource,
    decimal EffectiveRrp,
    bool HadCatalogueEntry,
    bool HadManualOverride);
