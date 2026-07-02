using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

public class DeterminismTests
{
    private readonly ExceptionSurfacingTestBuilder _builder = new();
    private static readonly RunId FixedRunId = RunId.FromGuid(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    [Fact]
    public void Double_surface_produces_identical_exception_ids_and_order()
    {
        var (run, _) = _builder.ExecuteAndSurface(
            ExceptionSurfacingFixtureLoader.Load("mixed-three-customers"),
            runId: FixedRunId);

        var surfacing = new ExceptionSurfacingService();
        var first = surfacing.Surface(run);
        var second = surfacing.Surface(run);

        ExceptionViewModelComparer.AreEquivalent(first, second).Should().BeTrue();
        first.GeneratedAt.Should().NotBe(second.GeneratedAt);
    }

    [Fact]
    public void Flat_exceptions_order_is_stable_across_calls()
    {
        var (run, _) = _builder.ExecuteAndSurface(
            Reconciliation.ReconciliationInputsFixtureLoader.Load("quantity-mismatch"),
            runId: FixedRunId);

        var surfacing = new ExceptionSurfacingService();
        var ids1 = surfacing.Surface(run).FlatExceptions().Select(e => e.Id.Value).ToList();
        var ids2 = surfacing.Surface(run).FlatExceptions().Select(e => e.Id.Value).ToList();

        ids1.Should().Equal(ids2);
    }
}
