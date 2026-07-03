using BillDrift.Domain.Billing;
using BillDrift.Domain.Mapping;

namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>Input snapshot for a catalogue reconciliation run.</summary>
public sealed record CatalogueReconciliationInputs(
    IReadOnlyList<StripeCatalogueProduct> StripeProducts,
    IReadOnlyList<StripeCataloguePrice> StripePrices,
    IReadOnlyList<ProductMapping> ProductMappings,
    IReadOnlyList<IntendedPrice> IntendedPrices,
    CatalogueInputReferences InputReferences);
