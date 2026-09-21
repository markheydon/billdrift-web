using BillDrift.Application.CatalogueReconciliation;
using BillDrift.Application.Mapping;
using BillDrift.Domain.CatalogueReconciliation;

namespace BillDrift.Application.Tests.CatalogueReconciliation;

public class CatalogueReconciliationEngineTests
{
    private static CatalogueReconciliationEngine CreateEngine() =>
        new(new ProductMappingResolver());

    [Fact]
    public void Clean_match_produces_zero_exceptions()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.CleanMatch());
        Assert.Empty(run.Exceptions);
    }

    [Fact]
    public void Missing_product_emits_MissingProduct_exception()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.MissingProduct());
        Assert.Contains(run.Exceptions, e => e.Type == CatalogueExceptionType.MissingProduct);
        Assert.Contains(run.ProposedFixes, f => f.ActionType == CatalogueProposedActionType.CreateProduct);
    }

    [Fact]
    public void Missing_price_emits_MissingPrice_exception()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.MissingPrice());
        Assert.Contains(run.Exceptions, e => e.Type == CatalogueExceptionType.MissingPrice);
        Assert.Contains(run.ProposedFixes, f => f.ActionType == CatalogueProposedActionType.CreatePrice);
    }

    [Fact]
    public void Incorrect_price_emits_IncorrectPrice_and_replacement_fix()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.IncorrectPrice());
        Assert.Contains(run.Exceptions, e => e.Type == CatalogueExceptionType.IncorrectPrice);
        Assert.Contains(run.ProposedFixes, f => f.ActionType == CatalogueProposedActionType.CreateReplacementPrice);
    }

    [Fact]
    public void Duplicate_products_emit_manual_cleanup_only()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.DuplicateProducts());
        Assert.Contains(run.Exceptions, e => e.Type == CatalogueExceptionType.DuplicateProduct);
        Assert.All(run.ProposedFixes, f => Assert.True(f.ActionType == CatalogueProposedActionType.FlagManualCleanup && !f.IsActionable));
    }

    [Fact]
    public void Duplicate_prices_emit_manual_cleanup_only()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.DuplicatePrices());
        Assert.Contains(run.Exceptions, e => e.Type == CatalogueExceptionType.DuplicatePrice);
        Assert.Contains(run.ProposedFixes, f => f.ActionType == CatalogueProposedActionType.FlagManualCleanup && !f.IsActionable);
    }

    [Fact]
    public void Pricing_reference_gap_recorded_without_price_checks()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.PricingReferenceGap());
        Assert.Contains(run.Exceptions, e => e.Type == CatalogueExceptionType.PricingReferenceGap);
        Assert.DoesNotContain(run.Exceptions, e => e.Type == CatalogueExceptionType.MissingPrice);
    }

    [Fact]
    public void Unmapped_stripe_product_is_reported()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.UnmappedStripeProduct());
        Assert.Contains(run.Exceptions, e => e.Type == CatalogueExceptionType.UnmappedCatalogueEntry);
    }

    [Fact]
    public void Manual_override_rrp_used_for_comparison()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.ManualOverrideRrp());
        Assert.Empty(run.Exceptions);
    }

    [Fact]
    public void Empty_catalogue_snapshot_fails_fast_without_producing_a_run()
    {
        var inputs = new CatalogueReconciliationInputs(
            [],
            [],
            [],
            [],
            new CatalogueInputReferences(null, null, null, null));

        var act = () => CreateEngine().Execute(inputs);

        Assert.Throws<CatalogueReconciliationValidationException>(act);
    }
}
