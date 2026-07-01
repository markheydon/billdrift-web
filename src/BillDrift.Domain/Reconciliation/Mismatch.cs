using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;

namespace BillDrift.Domain.Reconciliation;

/// <summary>
/// Typed references to domain entities involved in a <see cref="Mismatch"/>, enabling traceability without untyped ID strings.
/// </summary>
/// <param name="SupplierCostLineId">Supplier cost line involved in the mismatch, if any.</param>
/// <param name="SubscriptionLineId">Subscription management line involved, if any.</param>
/// <param name="IntendedPriceId">Intended price record involved, if any.</param>
/// <param name="StripeBillingItemId">Stripe billing item involved, if any.</param>
public sealed record MismatchEntityRefs(
    SupplierCostLineId? SupplierCostLineId = null,
    MicrosoftSubscriptionLineId? SubscriptionLineId = null,
    IntendedPriceId? IntendedPriceId = null,
    StripeBillingItemId? StripeBillingItemId = null);

/// <summary>
/// A detected drift between supplier cost, subscription truth, intended pricing, and Stripe customer billing.
/// Produced deterministically by a <see cref="ReconciliationRun"/> with operator-facing descriptions.
/// </summary>
/// <param name="Id">Unique identifier for this mismatch within the run.</param>
/// <param name="Type">Category of drift (quantity, price, mapping, etc.).</param>
/// <param name="Severity">Operator-facing severity level.</param>
/// <param name="Customer">Customer affected by this mismatch, when identifiable.</param>
/// <param name="CommercialKey">Commercial key context for price or product mismatches.</param>
/// <param name="InvolvedEntityIds">Typed references to the domain entities compared.</param>
/// <param name="ExpectedValue">Human-readable expected value from source-of-truth or intended pricing.</param>
/// <param name="ActualValue">Human-readable actual value found in Stripe or another domain.</param>
/// <param name="Description">Operator-facing explanation of the drift and suggested review steps.</param>
public sealed record Mismatch(
    MismatchId Id,
    MismatchType Type,
    MismatchSeverity Severity,
    CustomerIdentity? Customer,
    CommercialKey? CommercialKey,
    MismatchEntityRefs InvolvedEntityIds,
    string? ExpectedValue,
    string? ActualValue,
    string Description);
