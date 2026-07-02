using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation.Stages;

/// <summary>
/// Sorts match groups, mismatches, and proposed changes into deterministic output order (FR-019).
/// </summary>
public sealed class OutputOrderingStage : IReconciliationStage
{
    /// <inheritdoc />
    public void Execute(ReconciliationContext context)
    {
        context.MatchGroups.Sort(CompareMatchGroups);
        context.Mismatches.Sort(CompareMismatches);
        context.ProposedChanges.Sort(CompareProposedChanges);
    }

    private static int CompareMatchGroups(EntityMatchGroup a, EntityMatchGroup b)
    {
        var mexCompare = string.Compare(a.Customer.MexId.Value, b.Customer.MexId.Value, StringComparison.Ordinal);
        if (mexCompare != 0)
        {
            return mexCompare;
        }

        var keyA = a.CommercialKey;
        var keyB = b.CommercialKey;
        if (!keyA.HasValue && !keyB.HasValue)
        {
            return 0;
        }

        if (!keyA.HasValue)
        {
            return 1;
        }

        if (!keyB.HasValue)
        {
            return -1;
        }

        var offerCompare = string.Compare(keyA.Value.OfferId.Value, keyB.Value.OfferId.Value, StringComparison.Ordinal);
        if (offerCompare != 0)
        {
            return offerCompare;
        }

        var skuCompare = string.Compare(keyA.Value.SkuId.Value, keyB.Value.SkuId.Value, StringComparison.Ordinal);
        if (skuCompare != 0)
        {
            return skuCompare;
        }

        var termCompare = ((int)keyA.Value.Term).CompareTo((int)keyB.Value.Term);
        if (termCompare != 0)
        {
            return termCompare;
        }

        return ((int)keyA.Value.Frequency).CompareTo((int)keyB.Value.Frequency);
    }

    private static int CompareMismatches(Mismatch a, Mismatch b)
    {
        var mexA = a.Customer?.MexId.Value ?? string.Empty;
        var mexB = b.Customer?.MexId.Value ?? string.Empty;
        var mexCompare = string.Compare(mexA, mexB, StringComparison.Ordinal);
        if (mexCompare != 0)
        {
            return mexCompare;
        }

        var keyCompare = CompareCommercialKeys(a.CommercialKey, b.CommercialKey);
        if (keyCompare != 0)
        {
            return keyCompare;
        }

        return ((int)a.Type).CompareTo((int)b.Type);
    }

    private static int CompareCommercialKeys(CommercialKey? a, CommercialKey? b)
    {
        if (!a.HasValue && !b.HasValue)
        {
            return 0;
        }

        if (!a.HasValue)
        {
            return 1;
        }

        if (!b.HasValue)
        {
            return -1;
        }

        var offerCompare = string.Compare(a.Value.OfferId.Value, b.Value.OfferId.Value, StringComparison.Ordinal);
        if (offerCompare != 0)
        {
            return offerCompare;
        }

        return string.Compare(a.Value.SkuId.Value, b.Value.SkuId.Value, StringComparison.Ordinal);
    }

    private static int CompareProposedChanges(ProposedChange a, ProposedChange b)
    {
        var orderCompare = a.ExecutionOrder.CompareTo(b.ExecutionOrder);
        return orderCompare != 0 ? orderCompare : string.Compare(a.IdempotencyKey.Value, b.IdempotencyKey.Value, StringComparison.Ordinal);
    }
}
