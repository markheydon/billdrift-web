using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Domain.Tests.Reconciliation;

public class ProposedChangeTests
{
    [Fact]
    public void IdempotencyKey_uses_run_mismatch_and_action_format()
    {
        var runId = RunId.FromGuid(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var mismatchId = MismatchId.FromGuid(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        var key = IdempotencyKey.Create(runId, mismatchId, ProposedActionType.UpdateQuantity);

        key.Value.Should().Be("11111111-1111-1111-1111-111111111111:22222222-2222-2222-2222-222222222222:UpdateQuantity");
    }

    [Fact]
    public void ProposedChange_supports_all_action_types()
    {
        var mismatchId = MismatchId.New();
        var runId = RunId.New();

        foreach (var action in Enum.GetValues<ProposedActionType>())
        {
            var change = new ProposedChange(
                ProposedChangeId.New(),
                IdempotencyKey.Create(runId, mismatchId, action),
                mismatchId,
                action,
                new ProposedChangeTarget(),
                new Dictionary<string, string> { ["quantity"] = "10" },
                action == ProposedActionType.CreateOrUpdateCatalogueEntry
                    ? new CatalogueEntryPayload(
                        null,
                        "Product",
                        CommercialKeyRoot.Create(OfferId.Create("O1"), SkuId.Create("S1")),
                        [new PriceTermKey(Term.Annual, BillingFrequency.Monthly)])
                    : null,
                0);

            change.ActionType.Should().Be(action);
        }
    }
}
