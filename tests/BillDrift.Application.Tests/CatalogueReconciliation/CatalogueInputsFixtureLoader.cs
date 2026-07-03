using BillDrift.Domain.CatalogueReconciliation;

namespace BillDrift.Application.Tests.CatalogueReconciliation;

/// <summary>Loads catalogue reconciliation input fixtures by scenario name.</summary>
public static class CatalogueInputsFixtureLoader
{
    private static readonly Dictionary<string, Func<CatalogueReconciliationInputs>> Scenarios =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["catalogue-clean-match"] = CatalogueReconciliationTestDataBuilder.CleanMatch,
            ["catalogue-missing-product"] = CatalogueReconciliationTestDataBuilder.MissingProduct,
            ["catalogue-missing-price"] = CatalogueReconciliationTestDataBuilder.MissingPrice,
            ["catalogue-incorrect-price"] = CatalogueReconciliationTestDataBuilder.IncorrectPrice,
            ["catalogue-duplicate-products"] = CatalogueReconciliationTestDataBuilder.DuplicateProducts,
            ["catalogue-duplicate-prices"] = CatalogueReconciliationTestDataBuilder.DuplicatePrices,
            ["catalogue-pricing-gap"] = CatalogueReconciliationTestDataBuilder.PricingReferenceGap,
            ["catalogue-unmapped-stripe"] = CatalogueReconciliationTestDataBuilder.UnmappedStripeProduct,
            ["catalogue-manual-override-rrp"] = CatalogueReconciliationTestDataBuilder.ManualOverrideRrp,
            ["catalogue-determinism"] = CatalogueReconciliationTestDataBuilder.IncorrectPrice
        };

    public static CatalogueReconciliationInputs Load(string scenarioName)
    {
        var key = scenarioName.Replace(".json", "", StringComparison.OrdinalIgnoreCase);
        if (Scenarios.TryGetValue(key, out var factory))
        {
            return factory();
        }

        throw new ArgumentException($"Unknown catalogue fixture scenario: {scenarioName}", nameof(scenarioName));
    }
}
