using BillDrift.Application.Classification;
using BillDrift.Application.Tests.Reconciliation;
using BillDrift.Domain.Classification;

namespace BillDrift.Application.Tests.Classification;

public sealed class ReconciliationItemRefFactoryTests
{
    [Fact]
    public void SupplierCostLine_UsesPrimarySupplierReference()
    {
        var inputs = ReconciliationTestDataBuilder.NonCspSupplierLine();
        var line = inputs.SupplierCostLines[0];

        var itemRef = ReconciliationItemRefFactory.FromSupplierCostLine(line);

        Assert.Equal(ReconciliationItemKind.SupplierCost, itemRef.Kind);
        Assert.StartsWith($"{line.Customer.MexId.Value}:supplier:", itemRef.StableKey);
        Assert.Equal(line.Customer.MexId, itemRef.CustomerMexId);
    }

    [Fact]
    public void SubscriptionLine_UsesOfferSkuAndCorrelation()
    {
        var inputs = ReconciliationTestDataBuilder.CleanMatchAllDomains();
        var line = inputs.SubscriptionLines[0];

        var itemRef = ReconciliationItemRefFactory.FromSubscriptionLine(line);

        Assert.Contains(":truth:", itemRef.StableKey);
        Assert.Contains(line.CommercialKeyRoot.OfferId.Value, itemRef.StableKey);
        Assert.Contains(line.CommercialKeyRoot.SkuId.Value, itemRef.StableKey);
    }

    [Fact]
    public void StripeBillingItem_UsesSubscriptionItemId()
    {
        var inputs = ReconciliationTestDataBuilder.CleanMatchAllDomains();
        var item = inputs.StripeItems[0];

        var itemRef = ReconciliationItemRefFactory.FromStripeBillingItem(item);

        Assert.Equal($"{item.Customer.MexId.Value}:stripe:{item.SubscriptionItemId.Value}", itemRef.StableKey);
    }

    [Fact]
    public void ExtractAll_IncludesAllDomains()
    {
        var inputs = ReconciliationTestDataBuilder.CleanMatchAllDomains();
        var refs = ReconciliationItemRefFactory.ExtractAll(inputs, ReconciliationTestDataBuilder.DefaultScope);

        Assert.Contains(refs, r => r.Kind == ReconciliationItemKind.SupplierCost);
        Assert.Contains(refs, r => r.Kind == ReconciliationItemKind.SubscriptionTruth);
        Assert.Contains(refs, r => r.Kind == ReconciliationItemKind.StripeBilling);
    }
}
