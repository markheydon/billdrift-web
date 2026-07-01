using BillDrift.Domain.Billing;
using BillDrift.Domain.Mapping;

namespace BillDrift.Domain.Reconciliation;

/// <summary>
/// Immutable snapshot of all normalized inputs for a reconciliation run.
/// Same input content produces the same deterministic output from the reconciliation engine.
/// </summary>
/// <param name="SupplierCostLines">Normalized supplier cost lines from Giacom billing PDFs.</param>
/// <param name="SubscriptionLines">Normalized subscription lines from Giacom Subscription Management.</param>
/// <param name="IntendedPrices">Resolved intended prices (manual overrides beat catalogue for duplicate keys).</param>
/// <param name="StripeItems">Normalized Stripe subscription items (customer billing source of truth).</param>
/// <param name="ProductMappings">Product name to Stripe ID mappings for supplier product resolution.</param>
public sealed record ReconciliationInputs(
    IReadOnlyList<SupplierCostLine> SupplierCostLines,
    IReadOnlyList<MicrosoftSubscriptionLine> SubscriptionLines,
    IReadOnlyList<IntendedPrice> IntendedPrices,
    IReadOnlyList<StripeBillingItem> StripeItems,
    IReadOnlyList<ProductMapping> ProductMappings);
