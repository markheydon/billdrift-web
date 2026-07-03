namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>Roll-up counts for a catalogue reconciliation run.</summary>
public sealed record CatalogueReconciliationSummary(
    int MappedProductsChecked,
    IReadOnlyDictionary<CatalogueExceptionType, int> ExceptionsByType,
    int ProposedFixesActionable,
    int ProposedFixesManualOnly,
    int UnmappedStripeProducts,
    int UnmappedStripePrices);
