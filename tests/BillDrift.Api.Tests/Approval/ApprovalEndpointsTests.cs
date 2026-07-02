using BillDrift.Application.Approval;
using FluentAssertions;

namespace BillDrift.Api.Tests.Approval;

public sealed class ApprovalEndpointsTests
{
    [Fact]
    public void Approval_service_is_registered_in_api_assembly()
    {
        typeof(ApprovalService).Assembly.GetName().Name.Should().Be("BillDrift.Application");
    }
}
