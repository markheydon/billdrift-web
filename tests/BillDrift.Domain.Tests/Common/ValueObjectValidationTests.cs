using BillDrift.Domain.Common;

namespace BillDrift.Domain.Tests.Common;

public class ValueObjectValidationTests
{
    [Fact]
    public void MexId_rejects_empty_value()
    {
        var ex = Assert.Throws<DomainValidationException>(() => _ = MexId.Create("  "));
        Assert.Equal(nameof(MexId.Value), ex.PropertyName);
    }

    [Fact]
    public void BillingPeriod_rejects_end_before_start()
    {
        var ex = Assert.Throws<DomainValidationException>(() =>
            _ = BillingPeriod.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)));
        Assert.Equal(nameof(BillingPeriod.End), ex.PropertyName);
    }

    [Fact]
    public void StripeCustomerId_requires_cus_prefix()
    {
        Assert.Throws<DomainValidationException>(() => _ = StripeCustomerId.Create("invalid_id"));
    }

    [Fact]
    public void Money_rejects_negative_amount_by_default()
    {
        Assert.Throws<DomainValidationException>(() => _ = Money.Create(-1m, CurrencyCode.Gbp));
    }
}
