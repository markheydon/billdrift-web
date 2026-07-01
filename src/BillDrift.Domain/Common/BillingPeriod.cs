namespace BillDrift.Domain.Common;

/// <summary>
/// Inclusive date range for a billing or reconciliation scope.
/// Used on supplier cost lines and as the temporal boundary for a <see cref="Reconciliation.ReconciliationRun"/>.
/// </summary>
/// <param name="Start">The first day of the billing period.</param>
/// <param name="End">The last day of the billing period.</param>
public readonly record struct BillingPeriod(DateOnly Start, DateOnly End)
{
    /// <summary>
    /// Creates a validated <see cref="BillingPeriod"/> ensuring the end date is not before the start date.
    /// </summary>
    /// <param name="start">The period start date.</param>
    /// <param name="end">The period end date.</param>
    /// <returns>A validated billing period.</returns>
    /// <exception cref="DomainValidationException">Thrown when <paramref name="end"/> is before <paramref name="start"/>.</exception>
    public static BillingPeriod Create(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new DomainValidationException(nameof(End), "Billing period end must be on or after start.");
        }

        return new BillingPeriod(start, end);
    }
}
