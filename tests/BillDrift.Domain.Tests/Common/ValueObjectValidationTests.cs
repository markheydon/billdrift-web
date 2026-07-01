using BillDrift.Domain.Common;
using FluentAssertions;

namespace BillDrift.Domain.Tests.Common;

public class ValueObjectValidationTests
{
    [Fact]
    public void MexId_rejects_empty_value()
    {
        var act = () => MexId.Create("  ");
        act.Should().Throw<DomainValidationException>()
            .Which.PropertyName.Should().Be(nameof(MexId.Value));
    }

    [Fact]
    public void BillingPeriod_rejects_end_before_start()
    {
        var act = () => BillingPeriod.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1));
        act.Should().Throw<DomainValidationException>()
            .Which.PropertyName.Should().Be(nameof(BillingPeriod.End));
    }

    [Fact]
    public void StripeCustomerId_requires_cus_prefix()
    {
        var act = () => StripeCustomerId.Create("invalid_id");
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Money_rejects_negative_amount_by_default()
    {
        var act = () => Money.Create(-1m, CurrencyCode.Gbp);
        act.Should().Throw<DomainValidationException>();
    }
}
