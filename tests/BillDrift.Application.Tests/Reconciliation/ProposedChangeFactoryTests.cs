using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.Reconciliation;

public class ProposedChangeFactoryTests
{
    private static ReconciliationEngine CreateEngine() =>
        new(new Mapping.ProductMappingResolver());

    [Fact]
    public void Idempotency_key_uses_run_mismatch_and_action_format()
    {
        var runId = RunId.FromGuid(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var engine = CreateEngine();
        var run = engine.Execute(new ReconciliationRequest(
            runId,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationTestDataBuilder.QuantityMismatch()));

        run.ProposedChanges.Should().NotBeEmpty();
        run.ProposedChanges[0].IdempotencyKey.Value.Should()
            .StartWith($"{runId.Value}:");
    }

    [Fact]
    public void Mapping_missing_produces_no_bill_impacting_actions()
    {
        var engine = CreateEngine();
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationTestDataBuilder.MappingMissing()));

        run.ProposedChanges.Should().BeEmpty();
        run.Mismatches.Should().Contain(m => m.Type == MismatchType.MappingMissing);
    }

    [Fact]
    public void Duplicate_stripe_produces_no_bill_impacting_actions()
    {
        var engine = CreateEngine();
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationTestDataBuilder.DuplicateStripeItems()));

        run.ProposedChanges.Should().BeEmpty();
        run.Mismatches.Should().Contain(m => m.Type == MismatchType.MappingAmbiguous);
    }

    [Fact]
    public void Subscription_truth_without_product_mapping_produces_no_bill_impacting_actions()
    {
        var engine = CreateEngine();
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationTestDataBuilder.SubscriptionTruthMappingMissing()));

        run.Mismatches.Should().Contain(m =>
            m.Type == MismatchType.MappingMissing &&
            m.InvolvedEntityIds.SubscriptionLineId != null);
        run.Mismatches.Should().NotContain(m => m.Type == MismatchType.MissingInStripe);
        run.ProposedChanges.Should().BeEmpty();
    }

    [Fact]
    public void Non_csp_subscription_truth_without_supplier_line_produces_no_bill_impacting_actions()
    {
        var engine = CreateEngine();
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationTestDataBuilder.SubscriptionTruthNonCspOnly()));

        run.Mismatches.Should().Contain(m =>
            m.Type == MismatchType.MappingMissing &&
            m.Description.StartsWith("Non-CSP line requires manual mapping:"));
        run.ProposedChanges.Should().BeEmpty();
    }
}
