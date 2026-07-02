using BillDrift.Application.Classification;
using BillDrift.Application.Tests.Reconciliation;
using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using FluentAssertions;

namespace BillDrift.Application.Tests.Classification;

public sealed class ClassificationRuleEngineTests
{
    private static readonly ProductCategoryRule Microsoft365Rule = new(
        "OFFER-MS365",
        ProductCategoryMatchKind.OfferIdPrefix,
        ProductCategory.Microsoft365);

    private readonly ClassificationRuleEngine _engine = new();

    [Fact]
    public void Evaluate_MicrosoftCsp_HighConfidence_WhenAllSignalsPresent()
    {
        var itemRef = ReconciliationItemRefFactory.FromSubscriptionLine(
            ReconciliationTestDataBuilder.CleanMatchAllDomains().SubscriptionLines[0]);

        var signals = new ClassificationSignals(
            itemRef,
            MexId.Create("MEX-TEST-001"),
            HasOfferSku: true,
            OfferId.Create("OFFER-MS365-BB"),
            SkuId.Create("SKU-MS365-BB"),
            "Microsoft 365 Business Basic",
            InSubscriptionTruth: true,
            InIntendedPriceList: true,
            HasSupplierCostEvidence: true,
            HasStripeBillingOnly: false,
            ProductCategory.Microsoft365,
            ProductClassification.Csp);

        var result = _engine.Evaluate(
            signals,
            new ClassificationRuleConfiguration([], [Microsoft365Rule]),
            activeOverride: null,
            DateTimeOffset.UtcNow);

        result.Classification.Should().Be(ReconciliationItemClassification.MicrosoftCsp);
        result.Confidence.Should().Be(ClassificationConfidence.High);
        result.RuleBasis.Should().Contain("OfferSku");
    }

    [Fact]
    public void Evaluate_NonCspSupplier_WhenSupplierOnly()
    {
        var inputs = ReconciliationTestDataBuilder.NonCspSupplierLine();
        var itemRef = ReconciliationItemRefFactory.FromSupplierCostLine(inputs.SupplierCostLines[0]);

        var signals = new ClassificationSignals(
            itemRef,
            MexId.Create("MEX-TEST-001"),
            HasOfferSku: false,
            null,
            null,
            "Non-CSP Software",
            InSubscriptionTruth: false,
            InIntendedPriceList: false,
            HasSupplierCostEvidence: true,
            HasStripeBillingOnly: false,
            ProductCategory.Other,
            ProductClassification.NonCsp);

        var result = _engine.Evaluate(
            signals,
            ClassificationRuleConfiguration.Default,
            activeOverride: null,
            DateTimeOffset.UtcNow);

        result.Classification.Should().Be(ReconciliationItemClassification.NonCspSupplier);
        result.Confidence.Should().Be(ClassificationConfidence.High);
        result.RuleBasis.Should().Be("NonCsp:SupplierOnly");
    }

    [Fact]
    public void Evaluate_Internal_WhenConfiguredMexId()
    {
        var mexId = MexId.Create("INTERNAL-MEX-001");
        var itemRef = ReconciliationItemRef.Create(
            ReconciliationItemKind.SubscriptionTruth,
            $"{mexId.Value}:truth:test",
            mexId);

        var signals = new ClassificationSignals(
            itemRef,
            mexId,
            false,
            null,
            null,
            null,
            false,
            false,
            false,
            false,
            ProductCategory.Other,
            null);

        var config = new ClassificationRuleConfiguration([mexId], []);
        var result = _engine.Evaluate(signals, config, null, DateTimeOffset.UtcNow);

        result.Classification.Should().Be(ReconciliationItemClassification.Internal);
        result.RuleBasis.Should().Contain("InternalMexId");
    }

    [Fact]
    public void Evaluate_ConservativeDefault_LowConfidence_OnPartialSku()
    {
        var itemRef = ReconciliationItemRef.Create(
            ReconciliationItemKind.SubscriptionTruth,
            "MEX-TEST-001:truth:partial",
            MexId.Create("MEX-TEST-001"));

        var signals = new ClassificationSignals(
            itemRef,
            MexId.Create("MEX-TEST-001"),
            HasOfferSku: true,
            OfferId.Create("PARTIAL-OFFER"),
            SkuId.Create("PARTIAL-SKU"),
            null,
            InSubscriptionTruth: false,
            InIntendedPriceList: false,
            HasSupplierCostEvidence: false,
            HasStripeBillingOnly: false,
            ProductCategory.Other,
            null);

        var result = _engine.Evaluate(
            signals,
            ClassificationRuleConfiguration.Default,
            null,
            DateTimeOffset.UtcNow);

        result.Classification.Should().Be(ReconciliationItemClassification.NonCspSupplier);
        result.Confidence.Should().Be(ClassificationConfidence.Low);
        result.RuleBasis.Should().StartWith("ConservativeDefault:");
    }
}
