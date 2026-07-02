using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation.ExceptionSurfacing.Detection;

/// <summary>
/// Detects Mex ID conflicts across subscription truth, Stripe, and supplier cost on the same match group.
/// </summary>
public sealed class MexIdMismatchDetector
{
    /// <summary>
    /// Finds pairwise Mex ID conflicts within match groups and by shared Stripe customer ID.
    /// </summary>
    public IReadOnlyList<SurfacedException> Detect(SurfacingContext context)
    {
        var results = new List<SurfacedException>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in context.Run.MatchGroups)
        {
            var mexIds = CollectMexIds(group);
            if (mexIds.Count < 2)
            {
                continue;
            }

            var key = group.Id.Value.ToString();
            if (!seen.Add(key))
            {
                continue;
            }

            results.Add(CreateException(context, group.Customer, key, group.Id));
        }

        foreach (var stripeItem in context.Run.Inputs.StripeItems)
        {
            if (stripeItem.Customer.StripeCustomerId is null)
            {
                continue;
            }

            var relatedSubscriptions = context.Run.Inputs.SubscriptionLines
                .Where(sl => sl.Customer.StripeCustomerId == stripeItem.Customer.StripeCustomerId)
                .ToList();

            foreach (var subscription in relatedSubscriptions)
            {
                if (subscription.Customer.MexId == stripeItem.Customer.MexId)
                {
                    continue;
                }

                var key = $"{stripeItem.Customer.StripeCustomerId.Value}:{subscription.Id.Value}";
                if (!seen.Add(key))
                {
                    continue;
                }

                results.Add(CreateException(
                    context,
                    subscription.Customer,
                    key,
                    context.Run.MatchGroups.FirstOrDefault(g => g.SubscriptionLine?.Id == subscription.Id)?.Id));
            }
        }

        return results;
    }

    private static SurfacedException CreateException(
        SurfacingContext context,
        Domain.Common.CustomerIdentity customer,
        string entityRef,
        MatchGroupId? groupId) =>
        new(
            SurfacedExceptionId.FromDerived(context.Run.Id, "MexIdMismatch", entityRef),
            ExceptionCategory.MexIdMismatch,
            ReconciliationDomain.SupplierCostVsMapping,
            ExceptionSeverity.Error,
            customer,
            null,
            "Customer Mex ID differs across data sources.",
            [],
            RequiresActionNow: false,
            null,
            0,
            [],
            groupId);

    private static HashSet<string> CollectMexIds(EntityMatchGroup group)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Add(ids, group.Customer.MexId.Value);
        Add(ids, group.SubscriptionLine?.Customer.MexId.Value);
        Add(ids, group.StripeItem?.Customer.MexId.Value);
        Add(ids, group.SupplierCostLine?.Customer.MexId.Value);
        return ids;
    }

    private static void Add(HashSet<string> set, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            set.Add(value);
        }
    }
}
