using BillDrift.Domain.Approval;

namespace BillDrift.Domain.Tests.Approval;

public sealed class ApprovalTypesTests
{
    [Fact]
    public void ApprovalProposalId_round_trips_guid()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Assert.Equal(id, ApprovalProposalId.FromGuid(id).Value);
    }
}
