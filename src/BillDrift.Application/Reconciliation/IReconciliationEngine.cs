using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation;

/// <summary>
/// Input snapshot for a reconciliation run, bundling scope, normalized inputs, and optional tuning parameters.
/// </summary>
/// <param name="RunId">Optional run identifier; a new <see cref="RunId"/> is generated when <c>null</c>.</param>
/// <param name="Scope">Billing period that bounds which charges and subscriptions are in scope.</param>
/// <param name="Inputs">Immutable snapshot of normalized supplier, subscription, Stripe, and price data.</param>
/// <param name="Options">Optional flags and tolerances that control mismatch detection behaviour.</param>
public sealed record ReconciliationRequest(
    RunId? RunId,
    BillingPeriod Scope,
    ReconciliationInputs Inputs,
    ReconciliationOptions? Options = null);

/// <summary>
/// Tuning parameters for mismatch detection and proposed-change generation during reconciliation.
/// </summary>
/// <param name="IncludeNonCspProducts">When <c>false</c>, lines classified as non-CSP are excluded from comparison.</param>
/// <param name="IncludeInactiveSubscriptions">When <c>false</c>, only active subscriptions are considered for quantity checks.</param>
/// <param name="PriceTolerance">Absolute monetary tolerance when comparing Stripe unit amounts to intended prices.</param>
/// <param name="ProposeCatalogueChanges">When <c>true</c>, emits catalogue-gap proposals for missing Stripe price mappings.</param>
public sealed record ReconciliationOptions(
    bool IncludeNonCspProducts = false,
    bool IncludeInactiveSubscriptions = false,
    Money PriceTolerance = default,
    bool ProposeCatalogueChanges = true);

/// <summary>
/// Executes deterministic billing drift reconciliation over normalized input snapshots.
/// </summary>
public interface IReconciliationEngine
{
    /// <summary>
    /// Produces a <see cref="ReconciliationRun"/> from immutable inputs.
    /// Identical inputs and options yield equivalent mismatches and proposed changes.
    /// </summary>
    /// <param name="request">Scope, inputs, and optional tuning for this reconciliation run.</param>
    /// <returns>A completed run with detected mismatches and proposed corrective actions.</returns>
    ReconciliationRun Execute(ReconciliationRequest request);
}

/// <summary>
/// Thrown when reconciliation encounters an internal invariant violation that should not occur with valid inputs.
/// </summary>
public sealed class ReconciliationException : Exception
{
    /// <summary>
    /// Creates an exception describing an unexpected reconciliation failure.
    /// </summary>
    /// <param name="message">Human-readable explanation of the invariant violation.</param>
    public ReconciliationException(string message) : base(message)
    {
    }
}
