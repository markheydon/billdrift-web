using BillDrift.Api.History;

namespace BillDrift.Api.Tests.History;

public sealed class RunHistoryEndpointsTests
{
    [Fact]
    public void RunHistoryEndpoints_type_exists()
    {
        Assert.NotNull(typeof(RunHistoryEndpoints));
    }
}
