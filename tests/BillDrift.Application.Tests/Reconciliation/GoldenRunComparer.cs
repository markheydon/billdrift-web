using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Tests.Reconciliation;

/// <summary>
/// Compares reconciliation runs for deterministic mismatch set equivalence per quickstart.md.
/// </summary>
public static class GoldenRunComparer
{
    /// <summary>
    /// Signature tuple for mismatch comparison.
    /// </summary>
    public sealed record MismatchSignature(
        MismatchType Type,
        string? MexId,
        string? CommercialKey,
        string Description);

    /// <summary>
    /// Extracts comparable mismatch signatures from a reconciliation run.
    /// </summary>
    public static IReadOnlyList<MismatchSignature> ExtractSignatures(ReconciliationRun run) =>
        run.Mismatches
            .Select(m => new MismatchSignature(
                m.Type,
                m.Customer?.MexId.Value,
                FormatCommercialKey(m.CommercialKey),
                m.Description))
            .OrderBy(s => s.MexId, StringComparer.Ordinal)
            .ThenBy(s => s.CommercialKey, StringComparer.Ordinal)
            .ThenBy(s => s.Type)
            .ThenBy(s => s.Description, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Returns true when two runs have equivalent mismatch signatures.
    /// </summary>
    public static bool AreEquivalent(ReconciliationRun a, ReconciliationRun b) =>
        ExtractSignatures(a).SequenceEqual(ExtractSignatures(b));

    private static string? FormatCommercialKey(CommercialKey? key) =>
        key.HasValue ? $"{key.Value.OfferId.Value}/{key.Value.SkuId.Value}" : null;
}
