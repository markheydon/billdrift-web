using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

/// <summary>
/// Loads exception surfacing fixture scenarios by name.
/// </summary>
public static class ExceptionSurfacingFixtureLoader
{
    private static readonly Dictionary<string, Func<ReconciliationInputs>> Scenarios = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mixed-three-customers"] = ExceptionSurfacingTestDataBuilder.MixedThreeCustomers,
        ["suppression-mapping-root-cause"] = ExceptionSurfacingTestDataBuilder.SuppressionMappingRootCause,
        ["catalogue-consolidation"] = ExceptionSurfacingTestDataBuilder.CatalogueConsolidation,
        ["orphaned-stripe-item"] = ExceptionSurfacingTestDataBuilder.OrphanedStripeItem,
        ["mex-id-mismatch"] = ExceptionSurfacingTestDataBuilder.MexIdMismatch,
        ["low-confidence-no-action"] = ExceptionSurfacingTestDataBuilder.LowConfidenceNoAction,
        ["clean-run-empty"] = Reconciliation.ReconciliationTestDataBuilder.CleanMatchAllDomains
    };

    /// <summary>Loads reconciliation inputs for the named exception surfacing scenario.</summary>
    public static ReconciliationInputs Load(string scenarioName)
    {
        var key = scenarioName.Replace(".json", "", StringComparison.OrdinalIgnoreCase);
        if (Scenarios.TryGetValue(key, out var factory))
        {
            return factory();
        }

        throw new ArgumentException($"Unknown exception surfacing fixture scenario: {scenarioName}", nameof(scenarioName));
    }
}
