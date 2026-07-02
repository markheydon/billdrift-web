using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.Reconciliation;

public class MismatchDetectorTests
{
    private static ReconciliationEngine CreateEngine() =>
        new(new Mapping.ProductMappingResolver());

    [Theory]
    [InlineData("missing-in-stripe", MismatchType.MissingInStripe)]
    [InlineData("quantity-mismatch", MismatchType.QuantityMismatch)]
    [InlineData("billing-frequency-mismatch", MismatchType.BillingFrequencyMismatch)]
    [InlineData("price-mismatch", MismatchType.PriceMismatch)]
    [InlineData("catalogue-missing", MismatchType.CatalogueMissing)]
    [InlineData("mapping-missing", MismatchType.MappingMissing)]
    [InlineData("duplicate-stripe-items", MismatchType.MappingAmbiguous)]
    public void Detects_expected_mismatch_type(string scenario, MismatchType expectedType)
    {
        var engine = CreateEngine();
        var inputs = ReconciliationInputsFixtureLoader.Load(scenario);
        var options = scenario == "price-mismatch"
            ? new ReconciliationOptions(PriceTolerance: Money.Gbp(0))
            : null;

        var run = engine.Execute(new ReconciliationRequest(
            RunId.FromGuid(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            ReconciliationTestDataBuilder.DefaultScope,
            inputs,
            options));

        run.Mismatches.Should().Contain(m => m.Type == expectedType);
    }
}
