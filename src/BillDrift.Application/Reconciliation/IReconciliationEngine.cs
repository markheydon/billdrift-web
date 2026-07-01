using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation;

public sealed record ReconciliationRequest(
    RunId? RunId,
    BillingPeriod Scope,
    ReconciliationInputs Inputs,
    ReconciliationOptions? Options = null);

public sealed record ReconciliationOptions(
    bool IncludeNonCspProducts = false,
    bool IncludeInactiveSubscriptions = false,
    Money PriceTolerance = default,
    bool ProposeCatalogueChanges = true);

public interface IReconciliationEngine
{
    ReconciliationRun Execute(ReconciliationRequest request);
}

public sealed class ReconciliationEngineStub : IReconciliationEngine
{
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

public sealed class ReconciliationException : Exception
{
    public ReconciliationException(string message) : base(message)
    {
    }
}
