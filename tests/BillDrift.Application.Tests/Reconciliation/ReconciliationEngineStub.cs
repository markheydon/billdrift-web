using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Tests.Reconciliation;

/// <summary>
/// Placeholder implementation of <see cref="IReconciliationEngine"/> that returns an empty run for scaffolding and tests.
/// </summary>
public sealed class ReconciliationEngineStub : IReconciliationEngine
{
    /// <inheritdoc />
    public ReconciliationRun Execute(ReconciliationRequest request)
    {
        var runId = request.RunId ?? RunId.New();
        return new ReconciliationRun(
            runId,
            DateTimeOffset.UtcNow,
            request.Scope,
            request.Inputs,
            [],
            [],
            []);
    }
}
