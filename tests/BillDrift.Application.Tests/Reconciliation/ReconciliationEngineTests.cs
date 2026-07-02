using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

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

        run.Mismatches.Should().BeEmpty();
        run.MatchGroups.Should().HaveCount(1);
        run.MatchGroups[0].Confidence.Should().Be(MatchConfidence.High);
    }

    [Fact]
    public void Missing_in_stripe_proposes_CreateMissingItem()
    {
        var run = CreateEngine().Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load("missing-in-stripe")));

        run.Mismatches.Should().Contain(m => m.Type == MismatchType.MissingInStripe);
        run.ProposedChanges.Should().Contain(p => p.ActionType == ProposedActionType.CreateMissingItem);
    }

    [Fact]
    public void Quantity_mismatch_proposes_UpdateQuantity()
    {
        var run = CreateEngine().Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load("quantity-mismatch")));

        run.Mismatches.Should().Contain(m => m.Type == MismatchType.QuantityMismatch);
        run.ProposedChanges.Should().Contain(p => p.ActionType == ProposedActionType.UpdateQuantity);
        run.ProposedChanges.Should().Contain(p =>
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

        run.Mismatches.Should().Contain(m => m.Type == MismatchType.BillingFrequencyMismatch);
    }

    [Fact]
    public void Price_mismatch_detected_with_zero_tolerance()
    {
        var run = CreateEngine().Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load("price-mismatch"),
            new ReconciliationOptions(PriceTolerance: Money.Gbp(0))));

        run.Mismatches.Should().Contain(m => m.Type == MismatchType.PriceMismatch);
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
        signatures.Should().Contain(s => s.Type == MismatchType.QuantityMismatch);
    }

    [Fact]
    public void Duplicate_stripe_items_emits_MappingAmbiguous()
    {
        var run = CreateEngine().Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationInputsFixtureLoader.Load("duplicate-stripe-items")));

        run.Mismatches.Should().Contain(m => m.Type == MismatchType.MappingAmbiguous);
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

        withoutClassification.Mismatches.Should().Contain(m => m.Type == MismatchType.MissingInStripe);
        withEmptyClassification.Mismatches.Should().Contain(m => m.Type == MismatchType.MissingInStripe);
        withoutClassification.Mismatches.Select(m => m.Type)
            .Should()
            .BeEquivalentTo(withEmptyClassification.Mismatches.Select(m => m.Type));
    }
}
