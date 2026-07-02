using BillDrift.Application.Mapping;
using BillDrift.Application.Reconciliation;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

/// <summary>
/// Chains reconciliation engine execution with exception surfacing for integration tests.
/// </summary>
public sealed class ExceptionSurfacingTestBuilder
{
    private static readonly RunId DefaultRunId = RunId.FromGuid(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    private readonly ReconciliationEngine _engine = new(new ProductMappingResolver());
    private readonly ExceptionSurfacingService _surfacing = new();

    /// <summary>Executes reconciliation then surfaces exceptions for a named scenario.</summary>
    public ReconciliationExceptionViewModel SurfaceScenario(
        string scenarioName,
        ReconciliationOptions? options = null,
        RunId? runId = null)
    {
        try
        {
            var inputs = ExceptionSurfacingFixtureLoader.Load(scenarioName);
            return ExecuteAndSurface(inputs, options, runId).ViewModel;
        }
        catch (ArgumentException)
        {
            return SurfaceReconciliationScenario(scenarioName, options, runId);
        }
    }

    private ReconciliationExceptionViewModel SurfaceReconciliationScenario(
        string scenarioName,
        ReconciliationOptions? options = null,
        RunId? runId = null)
    {
        var inputs = Reconciliation.ReconciliationInputsFixtureLoader.Load(scenarioName);
        var run = _engine.Execute(new ReconciliationRequest(
            runId ?? DefaultRunId,
            Reconciliation.ReconciliationTestDataBuilder.DefaultScope,
            inputs,
            options));

        return _surfacing.Surface(run, options);
    }

    /// <summary>Surfaces exceptions from a pre-built reconciliation run.</summary>
    public ReconciliationExceptionViewModel SurfaceRun(
        ReconciliationRun run,
        ReconciliationOptions? options = null) =>
        _surfacing.Surface(run, options);

    /// <summary>Executes reconciliation and surfacing for explicit inputs.</summary>
    public (ReconciliationRun Run, ReconciliationExceptionViewModel ViewModel) ExecuteAndSurface(
        ReconciliationInputs inputs,
        ReconciliationOptions? options = null,
        RunId? runId = null)
    {
        var run = _engine.Execute(new ReconciliationRequest(
            runId ?? DefaultRunId,
            Reconciliation.ReconciliationTestDataBuilder.DefaultScope,
            inputs,
            options));

        return (run, _surfacing.Surface(run, options));
    }
}
