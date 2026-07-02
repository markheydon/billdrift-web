using BillDrift.Domain.Common;

namespace BillDrift.Application.Reconciliation.Stages;

/// <summary>
/// Validates reconciliation request inputs before pipeline execution.
/// </summary>
public sealed class InputValidationStage : IReconciliationStage
{
    /// <inheritdoc />
    public void Execute(ReconciliationContext context)
    {
        var request = context.Request;

        if (request.Inputs is null)
        {
            throw new DomainValidationException(nameof(request.Inputs), "Reconciliation inputs must not be null.");
        }

        if (request.Scope.End < request.Scope.Start)
        {
            throw new DomainValidationException(nameof(request.Scope), "Reconciliation scope end must be on or after start.");
        }
    }
}
