using BillDrift.Domain.Common;

namespace BillDrift.Application.Reconciliation.Matching;

/// <summary>
/// Resolves customer identity across billing domains using MexId as the primary correlation key.
/// </summary>
public sealed class CustomerMatcher
{
    /// <summary>
    /// Merges customer identity fields from multiple sources, prioritizing truth → Stripe → supplier.
    /// </summary>
    /// <param name="primary">Primary customer identity (typically from subscription truth).</param>
    /// <param name="secondary">Secondary customer identity to merge.</param>
    /// <returns>Merged customer identity with enriched display name and cross-reference IDs.</returns>
    public CustomerIdentity Merge(CustomerIdentity primary, CustomerIdentity? secondary)
    {
        if (secondary is null)
        {
            return primary;
        }

        return CustomerIdentity.Create(
            primary.MexId,
            primary.DisplayName ?? secondary.DisplayName,
            primary.TenantId ?? secondary.TenantId,
            primary.StripeCustomerId ?? secondary.StripeCustomerId);
    }

    /// <summary>
    /// Validates that a customer has a non-empty MexId.
    /// </summary>
    /// <param name="customer">Customer identity to validate.</param>
    /// <returns><see langword="true"/> when MexId is present.</returns>
    public bool HasValidMexId(CustomerIdentity customer) =>
        !string.IsNullOrWhiteSpace(customer.MexId.Value);
}
