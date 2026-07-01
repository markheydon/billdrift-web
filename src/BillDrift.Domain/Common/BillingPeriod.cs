namespace BillDrift.Domain.Common;

public readonly record struct BillingPeriod(DateOnly Start, DateOnly End)
{
    public static BillingPeriod Create(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new DomainValidationException(nameof(End), "Billing period end must be on or after start.");
        }

        return new BillingPeriod(start, end);
    }
}
