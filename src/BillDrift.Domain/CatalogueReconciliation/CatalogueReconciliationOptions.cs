namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>Options controlling catalogue reconciliation behaviour.</summary>
public sealed record CatalogueReconciliationOptions(
    bool IncludeArchivedPrices = false,
    bool IncludeNonCspProducts = true,
    bool ExactAmountMatch = true,
    string DefaultCurrency = "GBP");
