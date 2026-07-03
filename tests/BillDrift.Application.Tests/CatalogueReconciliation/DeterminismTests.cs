using BillDrift.Application.CatalogueReconciliation;
using BillDrift.Application.Mapping;
using BillDrift.Domain.CatalogueReconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.CatalogueReconciliation;

public class DeterminismTests
{
    [Fact]
    public void Identical_inputs_produce_identical_exception_signatures()
    {
        var engine = new CatalogueReconciliationEngine(new ProductMappingResolver());
        var inputs = CatalogueReconciliationTestDataBuilder.IncorrectPrice();
        var runId = CatalogueRunId.FromGuid(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));

        var first = engine.Execute(inputs, runId: runId);
        var second = engine.Execute(inputs, runId: runId);

        GoldenRunComparer.AreEquivalent(first, second).Should().BeTrue();
    }

    [Fact]
    public void Fixture_loader_determinism_scenario_is_stable()
    {
        var engine = new CatalogueReconciliationEngine(new ProductMappingResolver());
        var inputs = CatalogueInputsFixtureLoader.Load("catalogue-determinism");
        var runId = CatalogueRunId.FromGuid(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));

        var first = engine.Execute(inputs, runId: runId);
        var second = engine.Execute(inputs, runId: runId);

        GoldenRunComparer.AreEquivalent(first, second).Should().BeTrue();
    }
}
