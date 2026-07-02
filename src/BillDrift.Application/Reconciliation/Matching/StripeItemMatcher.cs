using BillDrift.Application.Reconciliation.Indexing;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;

namespace BillDrift.Application.Reconciliation.Matching;

/// <summary>
/// Result of matching Stripe billing items to a customer and commercial key.
/// </summary>
/// <param name="Item">The matched Stripe item when exactly one candidate exists.</param>
/// <param name="Candidates">All matching candidates (empty, one, or many).</param>
/// <param name="IsAmbiguous"><see langword="true"/> when multiple candidates match.</param>
public sealed record StripeItemMatchResult(
    StripeBillingItem? Item,
    IReadOnlyList<StripeBillingItem> Candidates,
    bool IsAmbiguous);

/// <summary>
/// Matches Stripe billing items to customer and commercial key (matching phases Phase 3).
/// </summary>
public sealed class StripeItemMatcher
{
    /// <summary>
    /// Finds Stripe billing items matching the customer and commercial key.
    /// </summary>
    /// <param name="catalogueIndex">Stripe catalogue index for item lookup.</param>
    /// <param name="customer">Customer identity to match.</param>
    /// <param name="key">Full commercial key including frequency.</param>
    /// <param name="includeInactive">When false, inactive items are excluded.</param>
    /// <returns>Match result with zero, one, or many candidates.</returns>
    public StripeItemMatchResult Match(
        StripeCatalogueIndex catalogueIndex,
        CustomerIdentity customer,
        CommercialKey key,
        bool includeInactive)
    {
        var candidates = catalogueIndex.FindItems(customer, key);
        if (candidates.Count == 0)
        {
            // Fall back to root match so frequency mismatches can be detected downstream.
            var root = CommercialKeyRoot.Create(key.OfferId, key.SkuId);
            candidates = catalogueIndex.FindItemsByRootIgnoringFrequency(customer, root);
        }

        return candidates.Count switch
        {
            0 => new StripeItemMatchResult(null, candidates, false),
            1 => new StripeItemMatchResult(candidates[0], candidates, false),
            _ => new StripeItemMatchResult(null, candidates, true)
        };
    }
}
