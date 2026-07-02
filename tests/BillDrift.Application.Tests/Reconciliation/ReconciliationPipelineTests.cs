using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.Reconciliation;

public class ReconciliationPipelineTests
{
    [Fact]
    public void Invalid_scope_throws_DomainValidationException()
    {
        var engine = new ReconciliationEngine(new Mapping.ProductMappingResolver());
        var scope = new BillingPeriod(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1));
        var request = new ReconciliationRequest(
            null,
            scope,
            ReconciliationTestDataBuilder.CleanMatchAllDomains());

        var act = () => engine.Execute(request);

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Empty_inputs_returns_empty_run()
    {
        var engine = new ReconciliationEngine(new Mapping.ProductMappingResolver());
        var request = new ReconciliationRequest(
            RunId.FromGuid(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            ReconciliationTestDataBuilder.DefaultScope,
            new Domain.Reconciliation.ReconciliationInputs([], [], [], [], []));

        var run = engine.Execute(request);

        run.Mismatches.Should().BeEmpty();
        run.MatchGroups.Should().BeEmpty();
        run.Id.Value.Should().NotBe(Guid.Empty);
    }
}
