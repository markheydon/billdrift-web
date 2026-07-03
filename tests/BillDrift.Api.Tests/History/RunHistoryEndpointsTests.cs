using BillDrift.Api.History;
using FluentAssertions;

namespace BillDrift.Api.Tests.History;

public sealed class RunHistoryEndpointsTests
{
    [Fact]
    public void RunHistoryEndpoints_type_exists()
    {
        typeof(RunHistoryEndpoints).Should().NotBeNull();
    }
}
