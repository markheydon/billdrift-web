using BillDrift.Domain.Approval;
using FluentAssertions;

namespace BillDrift.Domain.Tests.Approval;

public sealed class ApprovalTypesTests
{
    [Fact]
    public void ApprovalProposalId_round_trips_guid()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        ApprovalProposalId.FromGuid(id).Value.Should().Be(id);
    }
}
