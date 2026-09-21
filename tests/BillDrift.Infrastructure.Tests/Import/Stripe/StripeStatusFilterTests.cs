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
        Assert.Equal(expected, StripeStatusFilter.ShouldInclude(status, includeInactive));
    }

    [Fact]
    public void IsInactiveStatus_identifies_canceled()
    {
        Assert.True(StripeStatusFilter.IsInactiveStatus("canceled"));
        Assert.False(StripeStatusFilter.IsInactiveStatus("active"));
    }
}
