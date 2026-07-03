using BillDrift.Domain.CatalogueReconciliation;

namespace BillDrift.Application.Tests.CatalogueReconciliation;

/// <summary>Compares catalogue reconciliation runs for deterministic exception equivalence.</summary>
public static class GoldenRunComparer
{
    public sealed record ExceptionSignature(
        CatalogueExceptionType Type,
        string? OfferId,
        string? SkuId,
        string RuleId,
        string? ExpectedValue,
        string? ActualValue,
        string Description);

    public sealed record FixSignature(
        CatalogueProposedActionType ActionType,
        bool IsActionable,
        string? OfferId,
        string? SkuId);

    public static IReadOnlyList<ExceptionSignature> ExtractExceptionSignatures(CatalogueReconciliationRun run) =>
        run.Exceptions
            .Select(e => new ExceptionSignature(
                e.Type,
                e.CommercialKeyRoot?.OfferId.Value ?? e.CommercialKey?.OfferId.Value,
                e.CommercialKeyRoot?.SkuId.Value ?? e.CommercialKey?.SkuId.Value,
                e.RuleId,
                e.ExpectedValue,
                e.ActualValue,
                e.Description))
            .OrderBy(s => s.OfferId, StringComparer.Ordinal)
            .ThenBy(s => s.SkuId, StringComparer.Ordinal)
            .ThenBy(s => s.Type)
            .ThenBy(s => s.RuleId, StringComparer.Ordinal)
            .ThenBy(s => s.Description, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<FixSignature> ExtractFixSignatures(CatalogueReconciliationRun run) =>
        run.ProposedFixes
            .Select(f => new FixSignature(
                f.ActionType,
                f.IsActionable,
                f.CommercialKeyRoot?.OfferId.Value,
                f.CommercialKeyRoot?.SkuId.Value))
            .OrderBy(s => s.OfferId, StringComparer.Ordinal)
            .ThenBy(s => s.SkuId, StringComparer.Ordinal)
            .ThenBy(s => s.ActionType)
            .ToList();

    public static bool AreEquivalent(CatalogueReconciliationRun a, CatalogueReconciliationRun b) =>
        ExtractExceptionSignatures(a).SequenceEqual(ExtractExceptionSignatures(b))
        && ExtractFixSignatures(a).SequenceEqual(ExtractFixSignatures(b));
}
