using BillDrift.Application.Classification;
using BillDrift.Application.Tests.Reconciliation;
using BillDrift.Domain.Classification;
using FluentAssertions;

namespace BillDrift.Application.Tests.Classification;

public sealed class ReconciliationItemRefFactoryTests
{
    [Fact]
    public void SupplierCostLine_UsesPrimarySupplierReference()
    {
        var inputs = ReconciliationTestDataBuilder.NonCspSupplierLine();
        var line = inputs.SupplierCostLines[0];

        var itemRef = ReconciliationItemRefFactory.FromSupplierCostLine(line);

        itemRef.Kind.Should().Be(ReconciliationItemKind.SupplierCost);
        itemRef.StableKey.Should().StartWith($"{line.Customer.MexId.Value}:supplier:");
        itemRef.CustomerMexId.Should().Be(line.Customer.MexId);
    }

    [Fact]
    public void SubscriptionLine_UsesOfferSkuAndCorrelation()
    {
        var inputs = ReconciliationTestDataBuilder.CleanMatchAllDomains();
        var line = inputs.SubscriptionLines[0];

        var itemRef = ReconciliationItemRefFactory.FromSubscriptionLine(line);

        itemRef.StableKey.Should().Contain(":truth:");
        itemRef.StableKey.Should().Contain(line.CommercialKeyRoot.OfferId.Value);
        itemRef.StableKey.Should().Contain(line.CommercialKeyRoot.SkuId.Value);
    }

    [Fact]
    public void StripeBillingItem_UsesSubscriptionItemId()
    {
        var inputs = ReconciliationTestDataBuilder.CleanMatchAllDomains();
        var item = inputs.StripeItems[0];

        var itemRef = ReconciliationItemRefFactory.FromStripeBillingItem(item);

        itemRef.StableKey.Should().Be($"{item.Customer.MexId.Value}:stripe:{item.SubscriptionItemId.Value}");
    }

    [Fact]
    public void ExtractAll_IncludesAllDomains()
    {
        var inputs = ReconciliationTestDataBuilder.CleanMatchAllDomains();
        var refs = ReconciliationItemRefFactory.ExtractAll(inputs, ReconciliationTestDataBuilder.DefaultScope);

        refs.Should().Contain(r => r.Kind == ReconciliationItemKind.SupplierCost);
        refs.Should().Contain(r => r.Kind == ReconciliationItemKind.SubscriptionTruth);
        refs.Should().Contain(r => r.Kind == ReconciliationItemKind.StripeBilling);
    }
}
