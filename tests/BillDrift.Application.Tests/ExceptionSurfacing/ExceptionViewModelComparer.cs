using BillDrift.Application.Reconciliation.ExceptionSurfacing;

namespace BillDrift.Application.Tests.ExceptionSurfacing;

/// <summary>
/// Compares exception view models for determinism excluding <see cref="ReconciliationExceptionViewModel.GeneratedAt"/>.
/// </summary>
public static class ExceptionViewModelComparer
{
    /// <summary>Signature for deterministic exception comparison.</summary>
    public sealed record ExceptionSignature(
        string Id,
        string Category,
        string Severity,
        bool RequiresActionNow,
        string Explanation);

    /// <summary>Extracts comparable signatures from a view model.</summary>
    public static IReadOnlyList<ExceptionSignature> ExtractSignatures(
        ReconciliationExceptionViewModel viewModel) =>
        viewModel.FlatExceptions()
            .Select(e => new ExceptionSignature(
                e.Id.Value,
                e.Category.ToString(),
                e.Severity.ToString(),
                e.RequiresActionNow,
                e.Explanation))
            .ToList();

    /// <summary>Returns true when two view models are equivalent excluding GeneratedAt.</summary>
    public static bool AreEquivalent(
        ReconciliationExceptionViewModel a,
        ReconciliationExceptionViewModel b)
    {
        if (a.Summary.TotalCount != b.Summary.TotalCount ||
            a.Summary.RequiresActionNowCount != b.Summary.RequiresActionNowCount ||
            a.Summary.SuppressedCount != b.Summary.SuppressedCount)
        {
            return false;
        }

        return ExtractSignatures(a).SequenceEqual(ExtractSignatures(b));
    }
}
