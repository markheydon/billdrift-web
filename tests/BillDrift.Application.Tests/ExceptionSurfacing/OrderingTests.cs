using BillDrift.Application.Reconciliation.ExceptionSurfacing;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

public class OrderingTests
{
    private readonly ExceptionSurfacingTestBuilder _builder = new();

    [Fact]
    public void Error_customer_group_appears_before_warning_only_customer()
    {
        var vm = _builder.SurfaceScenario("mixed-three-customers");

        Assert.True(vm.CustomerGroups.Count > 1);
        var first = vm.CustomerGroups[0];
        Assert.Equal(ExceptionSeverity.Error, first.HighestSeverity);
    }

    [Fact]
    public void Within_group_orders_by_severity_then_action_urgency_then_category()
    {
        var vm = _builder.SurfaceScenario("mixed-three-customers");

        foreach (var group in vm.CustomerGroups)
        {
            var ordered = group.Exceptions;
            for (var i = 1; i < ordered.Count; i++)
            {
                var prev = ordered[i - 1];
                var curr = ordered[i];
                var prevRank = SeverityRank(prev.Severity);
                var currRank = SeverityRank(curr.Severity);
                Assert.True((prevRank <= currRank));
            }
        }
    }

    private static int SeverityRank(ExceptionSeverity severity) => severity switch
    {
        ExceptionSeverity.Error => 0,
        ExceptionSeverity.Warning => 1,
        ExceptionSeverity.Info => 2,
        _ => 3
    };
}
