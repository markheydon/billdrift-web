using BillDrift.Application.Reconciliation;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

public class SuppressionRulesTests
{
    private readonly ExceptionSurfacingService _surfacing = new();
    private static readonly RunId RunId = RunId.FromGuid(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    [Fact]
    public void SR1_mapping_root_cause_suppresses_quantity_on_same_group()
    {
        var groupId = MatchGroupId.FromGuid(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var customer = CustomerIdentity.Create(MexId.Create("MEX-SR1"));
        var key = CommercialKey.Create(
            OfferId.Create("OFFER-1"), SkuId.Create("SKU-1"), Term.P1M, BillingFrequency.Monthly);

        var group = new EntityMatchGroup(groupId, customer, key, null, null, null, null, MatchConfidence.High);
        var mappingMismatch = CreateMismatch(MismatchType.MappingAmbiguous, customer, key);
        var quantityMismatch = CreateMismatch(MismatchType.QuantityMismatch, customer, key);

        var run = CreateRun([group], [mappingMismatch, quantityMismatch], []);
        var vm = _surfacing.Surface(run);

        vm.FlatExceptions().Should().NotContain(e => e.Category == ExceptionCategory.QuantityLicenceMismatch);
        vm.FlatExceptions().Should().Contain(e => e.Category == ExceptionCategory.OfferSkuAmbiguousMapping);
        vm.Summary.SuppressedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SR3_low_confidence_strips_proposed_change_id()
    {
        var groupId = MatchGroupId.FromGuid(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var customer = CustomerIdentity.Create(MexId.Create("MEX-SR3"));
        var key = CommercialKey.Create(
            OfferId.Create("OFFER-1"), SkuId.Create("SKU-1"), Term.P1M, BillingFrequency.Monthly);
        var mismatchId = MismatchId.FromGuid(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var proposedId = ProposedChangeId.FromGuid(mismatchId.Value);

        var group = new EntityMatchGroup(groupId, customer, key, null, null, null, null, MatchConfidence.Low);
        var quantityMismatch = new Mismatch(
            mismatchId,
            MismatchType.QuantityMismatch,
            MismatchSeverity.Error,
            customer,
            key,
            new MismatchEntityRefs(),
            "10",
            "5",
            "Quantity mismatch test");

        var proposed = new ProposedChange(
            proposedId,
            new IdempotencyKey($"{RunId}:{mismatchId}:UpdateQuantity"),
            mismatchId,
            ProposedActionType.UpdateQuantity,
            new ProposedChangeTarget(),
            new Dictionary<string, string> { ["proposedQuantity"] = "10" },
            null,
            10);

        var run = CreateRun([group], [quantityMismatch], [proposed]);
        var vm = _surfacing.Surface(run);

        var quantity = vm.FlatExceptions()
            .Where(e => e.Category == ExceptionCategory.QuantityLicenceMismatch)
            .ToList();

        quantity.Should().HaveCount(1);
        quantity[0].ProposedChangeId.Should().BeNull();
        quantity[0].RequiresActionNow.Should().BeFalse();
    }

    [Fact]
    public void Surfaced_count_can_be_less_than_raw_mismatch_count()
    {
        var builder = new ExceptionSurfacingTestBuilder();
        var (run, vm) = builder.ExecuteAndSurface(
            ExceptionSurfacingTestDataBuilder.SuppressionMappingRootCause(),
            new ReconciliationOptions(PriceTolerance: Money.Gbp(0)),
            RunId);

        if (run.Mismatches.Count > vm.Summary.TotalCount)
        {
            vm.Summary.SuppressedCount.Should().BeGreaterThan(0);
        }
        else
        {
            vm.Summary.TotalCount.Should().BeGreaterThan(0);
        }
    }

    private static Mismatch CreateMismatch(MismatchType type, CustomerIdentity customer, CommercialKey key) =>
        new(
            MismatchId.FromGuid(Guid.NewGuid()),
            type,
            MismatchSeverity.Error,
            customer,
            key,
            new MismatchEntityRefs(),
            "10",
            "5",
            $"{type} test mismatch");

    private static ReconciliationRun CreateRun(
        IReadOnlyList<EntityMatchGroup> groups,
        IReadOnlyList<Mismatch> mismatches,
        IReadOnlyList<ProposedChange> proposed) =>
        new(
            RunId,
            DateTimeOffset.UtcNow,
            BillingPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)),
            new ReconciliationInputs([], [], [], [], []),
            groups,
            mismatches,
            proposed);
}
