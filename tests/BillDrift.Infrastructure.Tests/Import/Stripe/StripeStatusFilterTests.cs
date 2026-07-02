using BillDrift.Infrastructure.Import.Stripe;

namespace BillDrift.Infrastructure.Tests.Import.Stripe;

public class StripeStatusFilterTests
{
    [Theory]
    [InlineData("active", false, true)]
    [InlineData("trialing", false, true)]
    [InlineData("past_due", false, true)]
    [InlineData("canceled", false, false)]
    [InlineData("canceled", true, true)]
    public void ShouldInclude_respects_active_set_and_option(string status, bool includeInactive, bool expected)
    {
        StripeStatusFilter.ShouldInclude(status, includeInactive).Should().Be(expected);
    }

    [Fact]
    public void IsInactiveStatus_identifies_canceled()
    {
        StripeStatusFilter.IsInactiveStatus("canceled").Should().BeTrue();
        StripeStatusFilter.IsInactiveStatus("active").Should().BeFalse();
    }
}
