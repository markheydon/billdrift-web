using BillDrift.Application.Reconciliation;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

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

        vm.Summary.TotalCount.Should().Be(vm.FlatExceptions().Count);
        vm.Summary.BySeverity.Values.Sum().Should().Be(vm.Summary.TotalCount);
        vm.Summary.RequiresActionNowCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Clean_run_has_no_exceptions()
    {
        var vm = _builder.SurfaceScenario("clean-run-empty", runId: FixedRunId);

        vm.HasExceptions.Should().BeFalse();
        vm.Summary.TotalCount.Should().Be(0);
        vm.CustomerGroups.Should().BeEmpty();
    }

    [Fact]
    public void Quantity_mismatch_links_proposed_change_when_eligible()
    {
        var vm = _builder.SurfaceScenario("quantity-mismatch",
            new ReconciliationOptions(PriceTolerance: Money.Gbp(0)),
            FixedRunId);

        var exception = vm.FlatExceptions().Single(e => e.Category == ExceptionCategory.QuantityLicenceMismatch);
        exception.ProposedChangeId.Should().NotBeNull();
    }

    [Fact]
    public void Flat_exceptions_matches_grouped_order()
    {
        var vm = _builder.SurfaceScenario("mixed-three-customers", runId: FixedRunId);

        var flat = vm.FlatExceptions().Select(e => e.Id.Value).ToList();
        var grouped = vm.CustomerGroups.SelectMany(g => g.Exceptions).Select(e => e.Id.Value).ToList();
        flat.Should().Equal(grouped);
    }
}
