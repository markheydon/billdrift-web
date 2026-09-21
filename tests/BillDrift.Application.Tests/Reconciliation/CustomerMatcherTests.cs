using BillDrift.Application.Reconciliation.Matching;
using BillDrift.Domain.Common;

namespace BillDrift.Application.Tests.Reconciliation;

public class CustomerMatcherTests
{
    [Fact]
    public void Merge_prioritizes_primary_display_name()
    {
        var matcher = new CustomerMatcher();
        var primary = CustomerIdentity.Create(MexId.Create("MEX1"), "Primary Name");
        var secondary = CustomerIdentity.Create(MexId.Create("MEX1"), "Secondary Name", null, StripeCustomerId.Create("cus_abc"));

        var merged = matcher.Merge(primary, secondary);

        Assert.Equal("Primary Name", merged.DisplayName);
        Assert.Equal("cus_abc", merged.StripeCustomerId!.Value.Value);
    }

    [Fact]
    public void HasValidMexId_returns_true_for_valid_customer()
    {
        var matcher = new CustomerMatcher();
        Assert.True(matcher.HasValidMexId(CustomerIdentity.Create(MexId.Create("MEX1"))));
    }
}
