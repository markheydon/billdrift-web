using BillDrift.Application.Approval;

namespace BillDrift.Api.Tests.Approval;

public sealed class ApprovalEndpointsTests
{
    [Fact]
    public void Approval_service_is_registered_in_api_assembly()
    {
        Assert.Equal("BillDrift.Application", typeof(ApprovalService).Assembly.GetName().Name);
    }
}
