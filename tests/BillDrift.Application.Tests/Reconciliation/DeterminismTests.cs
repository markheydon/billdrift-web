using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.Reconciliation;

public class DeterminismTests
{
    [Fact]
    public void Duplicate_execute_produces_equivalent_mismatch_sets()
    {
        var engine = new ReconciliationEngine(new Mapping.ProductMappingResolver());
        var runId = RunId.FromGuid(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var inputs = ReconciliationTestDataBuilder.QuantityMismatch();
        var request = new ReconciliationRequest(
            runId,
            ReconciliationTestDataBuilder.DefaultScope,
            inputs);

        var run1 = engine.Execute(request);
        var run2 = engine.Execute(request);

        GoldenRunComparer.AreEquivalent(run1, run2).Should().BeTrue();
    }
}
