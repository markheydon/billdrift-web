using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Tests.Reconciliation;

/// <summary>
/// Loads reconciliation input fixtures by scenario name.
/// </summary>
public static class ReconciliationInputsFixtureLoader
{
    private static readonly Dictionary<string, Func<ReconciliationInputs>> Scenarios = new(StringComparer.OrdinalIgnoreCase)
    {
        ["clean-match-all-domains"] = ReconciliationTestDataBuilder.CleanMatchAllDomains,
        ["missing-in-stripe"] = ReconciliationTestDataBuilder.MissingInStripe,
        ["quantity-mismatch"] = ReconciliationTestDataBuilder.QuantityMismatch,
        ["billing-frequency-mismatch"] = ReconciliationTestDataBuilder.BillingFrequencyMismatch,
        ["price-mismatch"] = ReconciliationTestDataBuilder.PriceMismatch,
        ["catalogue-missing"] = ReconciliationTestDataBuilder.CatalogueMissing,
        ["mapping-missing"] = ReconciliationTestDataBuilder.MappingMissing,
        ["mapping-ambiguous"] = ReconciliationTestDataBuilder.DuplicateStripeItems,
        ["duplicate-stripe-items"] = ReconciliationTestDataBuilder.DuplicateStripeItems,
        ["non-csp-supplier-line"] = ReconciliationTestDataBuilder.NonCspSupplierLine,
        ["supplier-orphan-line"] = ReconciliationTestDataBuilder.SupplierOrphanLine
    };

    /// <summary>
    /// Loads reconciliation inputs for the named scenario.
    /// </summary>
    /// <param name="scenarioName">Fixture scenario name (without .json extension).</param>
    /// <returns>Reconciliation inputs for the scenario.</returns>
    /// <exception cref="ArgumentException">When scenario name is unknown.</exception>
    public static ReconciliationInputs Load(string scenarioName)
    {
        var key = scenarioName.Replace(".json", "", StringComparison.OrdinalIgnoreCase);
        if (Scenarios.TryGetValue(key, out var factory))
        {
            return factory();
        }

        throw new ArgumentException($"Unknown reconciliation fixture scenario: {scenarioName}", nameof(scenarioName));
    }

    /// <summary>
    /// Attempts to load a fixture JSON file path by extracting the scenario name from the file name.
    /// </summary>
    public static ReconciliationInputs LoadFromPath(string fixturePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(fixturePath);
        return Load(fileName);
    }
}
