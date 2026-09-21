using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

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

        Assert.NotEmpty(run.ProposedChanges);
        Assert.StartsWith($"{runId.Value}:", run.ProposedChanges[0].IdempotencyKey.Value);
    }

    [Fact]
    public void Mapping_missing_produces_no_bill_impacting_actions()
    {
        var engine = CreateEngine();
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationTestDataBuilder.MappingMissing()));

        Assert.Empty(run.ProposedChanges);
        Assert.Contains(run.Mismatches, m => m.Type == MismatchType.MappingMissing);
    }

    [Fact]
    public void Duplicate_stripe_produces_no_bill_impacting_actions()
    {
        var engine = CreateEngine();
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationTestDataBuilder.DuplicateStripeItems()));

        Assert.Empty(run.ProposedChanges);
        Assert.Contains(run.Mismatches, m => m.Type == MismatchType.MappingAmbiguous);
    }

    [Fact]
    public void Subscription_truth_without_product_mapping_produces_no_bill_impacting_actions()
    {
        var engine = CreateEngine();
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationTestDataBuilder.SubscriptionTruthMappingMissing()));

        Assert.Contains(run.Mismatches, m =>
            m.Type == MismatchType.MappingMissing &&
            m.InvolvedEntityIds.SubscriptionLineId != null);
        Assert.DoesNotContain(run.Mismatches, m => m.Type == MismatchType.MissingInStripe);
        Assert.Empty(run.ProposedChanges);
    }

    [Fact]
    public void Non_csp_subscription_truth_without_supplier_line_produces_no_bill_impacting_actions()
    {
        var engine = CreateEngine();
        var run = engine.Execute(new ReconciliationRequest(
            null,
            ReconciliationTestDataBuilder.DefaultScope,
            ReconciliationTestDataBuilder.SubscriptionTruthNonCspOnly()));

        Assert.Contains(run.Mismatches, m =>
            m.Type == MismatchType.MappingMissing &&
            m.Description.StartsWith("Non-CSP line requires manual mapping:"));
        Assert.Empty(run.ProposedChanges);
    }
}
