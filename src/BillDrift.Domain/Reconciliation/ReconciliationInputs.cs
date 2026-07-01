using BillDrift.Domain.Billing;
using BillDrift.Domain.Mapping;

namespace BillDrift.Domain.Reconciliation;

public sealed record ReconciliationInputs(
    IReadOnlyList<SupplierCostLine> SupplierCostLines,
    IReadOnlyList<MicrosoftSubscriptionLine> SubscriptionLines,
    IReadOnlyList<IntendedPrice> IntendedPrices,
    IReadOnlyList<StripeBillingItem> StripeItems,
    IReadOnlyList<ProductMapping> ProductMappings);
