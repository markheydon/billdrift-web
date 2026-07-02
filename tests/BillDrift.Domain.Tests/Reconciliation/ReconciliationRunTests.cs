using BillDrift.Application.Reconciliation;
using BillDrift.Application.Tests.Reconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Domain.Tests.Reconciliation;

public class ReconciliationRunTests
{
    [Fact]
    public void ReconciliationRun_composes_inputs_match_groups_and_mismatches()
    {
        var scope = BillingPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var inputs = new ReconciliationInputs([], [], [], [], []);
        var run = new ReconciliationRun(
            RunId.New(),
            DateTimeOffset.UtcNow,
            scope,
            inputs,
            [],
            [],
            []);

        run.Scope.Start.Should().Be(new DateOnly(2026, 1, 1));
        run.Inputs.ProductMappings.Should().BeEmpty();
    }

    [Fact]
    public void EntityMatchGroup_supports_optional_domain_entities()
    {
        var customer = CustomerIdentity.Create(MexId.Create("MEX1"));
        var group = new EntityMatchGroup(
            MatchGroupId.New(),
            customer,
            null,
            null,
            null,
            null,
            null,
            MatchConfidence.None);

        group.Customer.MexId.Should().Be(MexId.Create("MEX1"));
    }
}

public class DeterminismTests
{
    [Fact]
    public void ReconciliationEngineStub_produces_equivalent_empty_runs_for_same_inputs()
    {
        var engine = new ReconciliationEngineStub();
        var scope = BillingPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var runId = RunId.FromGuid(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var inputs = new ReconciliationInputs([], [], [], [], []);
        var request = new ReconciliationRequest(runId, scope, inputs);

        var run1 = engine.Execute(request);
        var run2 = engine.Execute(request);

        run1.Id.Should().Be(run2.Id);
        run1.Mismatches.Should().BeEquivalentTo(run2.Mismatches);
        run1.ProposedChanges.Should().BeEquivalentTo(run2.ProposedChanges);
    }
}
