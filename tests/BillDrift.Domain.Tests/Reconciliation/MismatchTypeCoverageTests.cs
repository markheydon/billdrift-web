using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Domain.Tests.Reconciliation;

public class MismatchTypeCoverageTests
{
    public static IEnumerable<object[]> MismatchTypes() =>
        Enum.GetValues<MismatchType>().Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(MismatchTypes))]
    public void Each_mismatch_type_can_be_instantiated(MismatchType type)
    {
        var mismatch = new Mismatch(
            MismatchId.New(),
            type,
            MismatchSeverity.Warning,
            CustomerIdentity.Create(MexId.Create("MEX1")),
            null,
            new MismatchEntityRefs(),
            "expected",
            "actual",
            $"Example mismatch for {type}");

        mismatch.Type.Should().Be(type);
        mismatch.Description.Should().Contain(type.ToString());
    }

    [Fact]
    public void QuantityMismatch_example_includes_entity_refs()
    {
        var supplierId = SupplierCostLineId.New();
        var stripeId = StripeBillingItemId.New();

        var mismatch = new Mismatch(
            MismatchId.New(),
            MismatchType.QuantityMismatch,
            MismatchSeverity.Error,
            CustomerIdentity.Create(MexId.Create("MEX1")),
            CommercialKey.Create(
                OfferId.Create("OFFER-1"),
                SkuId.Create("SKU-1"),
                Term.Annual,
                BillingFrequency.Monthly),
            new MismatchEntityRefs(supplierId, null, null, stripeId),
            "10",
            "5",
            "Licence count differs from Stripe quantity");

        mismatch.InvolvedEntityIds.StripeBillingItemId.Should().Be(stripeId);
    }
}
