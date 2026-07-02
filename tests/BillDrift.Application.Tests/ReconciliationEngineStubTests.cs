using BillDrift.Application.Reconciliation;
using BillDrift.Application.Tests.Reconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests;

public class ReconciliationEngineStubTests
{
    [Fact]
    public void Execute_returns_empty_reconciliation_run()
    {
        var engine = new ReconciliationEngineStub();
        var request = new ReconciliationRequest(
            null,
            BillingPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)),
            new ReconciliationInputs([], [], [], [], []));

        var run = engine.Execute(request);

        run.Mismatches.Should().BeEmpty();
        run.ProposedChanges.Should().BeEmpty();
        run.Id.Value.Should().NotBe(Guid.Empty);
    }
}
