using BillDrift.Application.Reconciliation.ExceptionSurfacing.Ordering;
using BillDrift.Domain.Common;

namespace BillDrift.Application.Reconciliation.ExceptionSurfacing.Phases;

/// <summary>Phase 4 — computes triage flags, summaries, grouping, and ordering.</summary>
public sealed class FinalizePhase
{
    private readonly ExceptionOrdering _ordering = new();

    /// <summary>Builds the final view model from post-suppression candidates.</summary>
    public ReconciliationExceptionViewModel Execute(SurfacingContext context)
    {
        var withFlags = context.Candidates
            .Select(c => c with { RequiresActionNow = ComputeRequiresActionNow(c, context) })
            .ToList();

        var orderedExceptions = _ordering.OrderExceptions(withFlags);
        var groups = BuildCustomerGroups(orderedExceptions);
        var orderedGroups = _ordering.OrderCustomerGroups(groups);
        var summary = BuildSummary(orderedExceptions, orderedGroups, context);

        return new ReconciliationExceptionViewModel(
            context.Run.Id,
            context.Run.Scope,
            DateTimeOffset.UtcNow,
            summary,
            orderedGroups);
    }

    private static bool ComputeRequiresActionNow(SurfacedException exception, SurfacingContext context)
    {
        if (exception.Severity != ExceptionSeverity.Error)
        {
            return false;
        }

        if (exception.ProposedChangeId is not null)
        {
            return true;
        }

        var billImpacting = exception.Category is ExceptionCategory.MissingBillingItem
            or ExceptionCategory.OrphanedBillingItem
            or ExceptionCategory.QuantityLicenceMismatch
            or ExceptionCategory.BillingFrequencyMismatch
            or ExceptionCategory.ProductMismatch;

        if (billImpacting)
        {
            if (exception.MatchGroupId is { } groupId &&
                context.MatchGroupById.TryGetValue(groupId, out var group) &&
                group.Confidence is MatchConfidence.Low or MatchConfidence.None)
            {
                return false;
            }

            return true;
        }

        var blockingSetup = exception.Category is ExceptionCategory.OfferSkuAmbiguousMapping
            or ExceptionCategory.MexIdMismatch
            or ExceptionCategory.StripeProductMissing
            or ExceptionCategory.StripePriceMissing
            or ExceptionCategory.NonCspManualReview;

        return blockingSetup;
    }

    private static int SeverityRank(ExceptionSeverity severity) => severity switch
    {
        ExceptionSeverity.Error => 0,
        ExceptionSeverity.Warning => 1,
        ExceptionSeverity.Info => 2,
        _ => 3
    };

    private static List<CustomerExceptionGroup> BuildCustomerGroups(
        IReadOnlyList<SurfacedException> exceptions)
    {
        return exceptions
            .GroupBy(e => e.Customer.MexId.Value, StringComparer.Ordinal)
            .Select(g =>
            {
                var customer = g.First().Customer;
                var display = customer.DisplayName ?? customer.MexId.Value;
                var bySeverity = g.GroupBy(e => e.Severity)
                    .ToDictionary(x => x.Key, x => x.Count());
                var highest = bySeverity.Keys.MinBy(SeverityRank);
                var actionCount = g.Count(e => e.RequiresActionNow);

                return new CustomerExceptionGroup(
                    customer,
                    display,
                    highest,
                    bySeverity,
                    actionCount,
                    g.ToList());
            })
            .ToList();
    }

    private static ExceptionRunSummary BuildSummary(
        IReadOnlyList<SurfacedException> exceptions,
        IReadOnlyList<CustomerExceptionGroup> groups,
        SurfacingContext context)
    {
        return new ExceptionRunSummary(
            exceptions.Count,
            exceptions.GroupBy(e => e.Severity).ToDictionary(g => g.Key, g => g.Count()),
            exceptions.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Count()),
            exceptions.GroupBy(e => e.Domain).ToDictionary(g => g.Key, g => g.Count()),
            groups.Count,
            exceptions.Count(e => e.RequiresActionNow),
            context.Suppressed.Count);
    }
}
