using BillDrift.Application.Reconciliation;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

public class ExceptionSurfacingServiceTests
{
    private readonly ExceptionSurfacingTestBuilder _builder = new();
    private static readonly RunId FixedRunId = RunId.FromGuid(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    [Fact]
    public void Mixed_three_customers_summary_counts_match_surfaced_exceptions()
    {
        var vm = _builder.SurfaceScenario("mixed-three-customers", runId: FixedRunId);

        Assert.Equal(vm.FlatExceptions().Count, vm.Summary.TotalCount);
        Assert.Equal(vm.Summary.TotalCount, vm.Summary.BySeverity.Values.Sum());
        Assert.True(vm.Summary.RequiresActionNowCount > 0);
    }

    [Fact]
    public void Clean_run_has_no_exceptions()
    {
        var vm = _builder.SurfaceScenario("clean-run-empty", runId: FixedRunId);

        Assert.False(vm.HasExceptions);
        Assert.Equal(0, vm.Summary.TotalCount);
        Assert.Empty(vm.CustomerGroups);
    }

    [Fact]
    public void Quantity_mismatch_links_proposed_change_when_eligible()
    {
        var vm = _builder.SurfaceScenario("quantity-mismatch",
            new ReconciliationOptions(PriceTolerance: Money.Gbp(0)),
            FixedRunId);

        var exception = vm.FlatExceptions().Single(e => e.Category == ExceptionCategory.QuantityLicenceMismatch);
        Assert.NotNull(exception.ProposedChangeId);
    }

    [Fact]
    public void Flat_exceptions_matches_grouped_order()
    {
        var vm = _builder.SurfaceScenario("mixed-three-customers", runId: FixedRunId);

        var flat = vm.FlatExceptions().Select(e => e.Id.Value).ToList();
        var grouped = vm.CustomerGroups.SelectMany(g => g.Exceptions).Select(e => e.Id.Value).ToList();
        Assert.Equal(grouped, flat);
    }
}
