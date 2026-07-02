using BillDrift.Domain.Common;

namespace BillDrift.Application.Reconciliation.ExceptionSurfacing.Ordering;

/// <summary>
/// Deterministic ordering for customer groups and within-group exceptions
/// per contracts/suppression-and-ordering-rules.md.
/// </summary>
public sealed class ExceptionOrdering
{
    private static readonly Dictionary<ExceptionCategory, int> CategoryPriority = new()
    {
        [ExceptionCategory.MissingBillingItem] = 10,
        [ExceptionCategory.OrphanedBillingItem] = 20,
        [ExceptionCategory.MexIdMismatch] = 30,
        [ExceptionCategory.OfferSkuAmbiguousMapping] = 40,
        [ExceptionCategory.ProductMismatch] = 50,
        [ExceptionCategory.QuantityLicenceMismatch] = 60,
        [ExceptionCategory.BillingFrequencyMismatch] = 70,
        [ExceptionCategory.StripePriceRrpMismatch] = 80,
        [ExceptionCategory.StripeProductMissing] = 90,
        [ExceptionCategory.StripePriceMissing] = 100,
        [ExceptionCategory.NonCspManualReview] = 120
    };

    private static int SeverityRank(ExceptionSeverity severity) => severity switch
    {
        ExceptionSeverity.Error => 0,
        ExceptionSeverity.Warning => 1,
        ExceptionSeverity.Info => 2,
        _ => 3
    };

    /// <summary>Orders customer groups for operator triage.</summary>
    public IReadOnlyList<CustomerExceptionGroup> OrderCustomerGroups(
        IReadOnlyList<CustomerExceptionGroup> groups) =>
        groups
            .OrderBy(g => SeverityRank(g.HighestSeverity))
            .ThenByDescending(g => g.RequiresActionNowCount)
            .ThenBy(g => g.Customer.MexId.Value, StringComparer.Ordinal)
            .Select(g => g with
            {
                Exceptions = OrderExceptions(g.Exceptions)
            })
            .ToList();

    /// <summary>Orders exceptions within a customer group.</summary>
    public IReadOnlyList<SurfacedException> OrderExceptions(IReadOnlyList<SurfacedException> exceptions) =>
        exceptions
            .OrderBy(e => SeverityRank(e.Severity))
            .ThenByDescending(e => e.RequiresActionNow)
            .ThenBy(e => CategorySortKey(e))
            .ThenBy(e => CommercialKeySort(e.Product?.CommercialKey))
            .ThenBy(e => e.Id.Value, StringComparer.Ordinal)
            .ToList();

    private static int CategorySortKey(SurfacedException exception)
    {
        if (exception.Category == ExceptionCategory.StripePriceRrpMismatch &&
            exception.Domain == ReconciliationDomain.PricingVsCatalogue)
        {
            return 110;
        }

        return CategoryPriority.GetValueOrDefault(exception.Category, 999);
    }

    private static string CommercialKeySort(CommercialKey? key) =>
        key.HasValue
            ? $"{key.Value.OfferId.Value}/{key.Value.SkuId.Value}/{key.Value.Term}/{key.Value.Frequency}"
            : "\uffff";
}
