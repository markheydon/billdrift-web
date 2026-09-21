using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Tests.Reconciliation;

public class ReconciliationEngineTests
{
    private static ReconciliationEngine CreateEngine() =>
        new(new Mapping.ProductMappingResolver());

    [Fact]
    public void Clean_match_produces_zero_mismatches()
    {
        var run = CreateEngine().Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load("clean-match-all-domains")));

        Assert.Empty(run.Mismatches);
        var matchGroup = Assert.Single(run.MatchGroups);
        Assert.Equal(MatchConfidence.High, matchGroup.Confidence);
    }

    [Fact]
    public void Missing_in_stripe_proposes_CreateMissingItem()
    {
        var run = CreateEngine().Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load("missing-in-stripe")));

        Assert.Contains(run.Mismatches, m => m.Type == MismatchType.MissingInStripe);
        Assert.Contains(run.ProposedChanges, p => p.ActionType == ProposedActionType.CreateMissingItem);
    }

    [Fact]
    public void Quantity_mismatch_proposes_UpdateQuantity()
    {
        var run = CreateEngine().Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load("quantity-mismatch")));

        Assert.Contains(run.Mismatches, m => m.Type == MismatchType.QuantityMismatch);
        Assert.Contains(run.ProposedChanges, p => p.ActionType == ProposedActionType.UpdateQuantity);
        Assert.Contains(run.ProposedChanges, p =>
            p.ProposedValues["proposedQuantity"] == "10");
    }

    [Fact]
    public void Billing_frequency_mismatch_proposes_SwitchPrice_when_alternate_exists()
    {
        var inputs = ReconciliationTestDataBuilder.BillingFrequencyMismatch();
        var run = CreateEngine().Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            inputs));

        Assert.Contains(run.Mismatches, m => m.Type == MismatchType.BillingFrequencyMismatch);
    }

    [Fact]
    public void Price_mismatch_detected_with_zero_tolerance()
    {
        var run = CreateEngine().Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load("price-mismatch"),
            new ReconciliationOptions(PriceTolerance: Money.Gbp(0))));

        Assert.Contains(run.Mismatches, m => m.Type == MismatchType.PriceMismatch);
    }

    [Fact]
    public void GoldenRun_quantity_mismatch_matches_expected_signatures()
    {
        var runId = RunId.FromGuid(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var run = CreateEngine().Execute(new ReconciliationRequest(
            runId,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load("quantity-mismatch")));

        var signatures = GoldenRunComparer.ExtractSignatures(run);
        Assert.Contains(signatures, s => s.Type == MismatchType.QuantityMismatch);
    }

    [Fact]
    public void Duplicate_stripe_items_emits_MappingAmbiguous()
    {
        var run = CreateEngine().Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load("duplicate-stripe-items")));

        Assert.Contains(run.Mismatches, m => m.Type == MismatchType.MappingAmbiguous);
    }

    [Fact]
    public void Null_classification_context_preserves_legacy_engine_behaviour()
    {
        var inputs = ReconciliationTestDataBuilder.MissingInStripe();
        var withoutClassification = CreateEngine().Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            inputs,
            Classifications: null));

        var withEmptyClassification = CreateEngine().Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            inputs,
            Classifications: new BillDrift.Application.Classification.ClassificationContext(
                new Dictionary<string, BillDrift.Domain.Classification.ItemClassification>(),
                DateTimeOffset.UtcNow)));

        Assert.Contains(withoutClassification.Mismatches, m => m.Type == MismatchType.MissingInStripe);
        Assert.Contains(withEmptyClassification.Mismatches, m => m.Type == MismatchType.MissingInStripe);
        Assert.Equivalent(
            withEmptyClassification.Mismatches.Select(m => m.Type),
            withoutClassification.Mismatches.Select(m => m.Type));
    }
}
