using BillDrift.Application.CatalogueReconciliation;
using BillDrift.Application.Mapping;
using BillDrift.Domain.CatalogueReconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.CatalogueReconciliation;

public class CatalogueReconciliationEngineTests
{
    private static CatalogueReconciliationEngine CreateEngine() =>
        new(new ProductMappingResolver());

    [Fact]
    public void Clean_match_produces_zero_exceptions()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.CleanMatch());
        run.Exceptions.Should().BeEmpty();
    }

    [Fact]
    public void Missing_product_emits_MissingProduct_exception()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.MissingProduct());
        run.Exceptions.Should().Contain(e => e.Type == CatalogueExceptionType.MissingProduct);
        run.ProposedFixes.Should().Contain(f => f.ActionType == CatalogueProposedActionType.CreateProduct);
    }

    [Fact]
    public void Missing_price_emits_MissingPrice_exception()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.MissingPrice());
        run.Exceptions.Should().Contain(e => e.Type == CatalogueExceptionType.MissingPrice);
        run.ProposedFixes.Should().Contain(f => f.ActionType == CatalogueProposedActionType.CreatePrice);
    }

    [Fact]
    public void Incorrect_price_emits_IncorrectPrice_and_replacement_fix()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.IncorrectPrice());
        run.Exceptions.Should().Contain(e => e.Type == CatalogueExceptionType.IncorrectPrice);
        run.ProposedFixes.Should().Contain(f => f.ActionType == CatalogueProposedActionType.CreateReplacementPrice);
    }

    [Fact]
    public void Duplicate_products_emit_manual_cleanup_only()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.DuplicateProducts());
        run.Exceptions.Should().Contain(e => e.Type == CatalogueExceptionType.DuplicateProduct);
        run.ProposedFixes.Should().OnlyContain(f => f.ActionType == CatalogueProposedActionType.FlagManualCleanup && !f.IsActionable);
    }

    [Fact]
    public void Duplicate_prices_emit_manual_cleanup_only()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.DuplicatePrices());
        run.Exceptions.Should().Contain(e => e.Type == CatalogueExceptionType.DuplicatePrice);
        run.ProposedFixes.Should().Contain(f => f.ActionType == CatalogueProposedActionType.FlagManualCleanup && !f.IsActionable);
    }

    [Fact]
    public void Pricing_reference_gap_recorded_without_price_checks()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.PricingReferenceGap());
        run.Exceptions.Should().Contain(e => e.Type == CatalogueExceptionType.PricingReferenceGap);
        run.Exceptions.Should().NotContain(e => e.Type == CatalogueExceptionType.MissingPrice);
    }

    [Fact]
    public void Unmapped_stripe_product_is_reported()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.UnmappedStripeProduct());
        run.Exceptions.Should().Contain(e => e.Type == CatalogueExceptionType.UnmappedCatalogueEntry);
    }

    [Fact]
    public void Manual_override_rrp_used_for_comparison()
    {
        var run = CreateEngine().Execute(CatalogueReconciliationTestDataBuilder.ManualOverrideRrp());
        run.Exceptions.Should().BeEmpty();
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

        act.Should().Throw<CatalogueReconciliationValidationException>();
    }
}
