using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.CatalogueReconciliation.Detection;

/// <summary>Creates <see cref="CatalogueProposedFix"/> records for catalogue exceptions.</summary>
public sealed class CatalogueProposedFixFactory
{
    /// <summary>Creates proposed fixes for the exceptions accumulated on the context.</summary>
    public void AttachFixes(CatalogueReconciliationContext context)
    {
        foreach (var exception in context.Exceptions)
        {
            var fix = CreateFix(context.RunId, exception);
            if (fix is not null)
            {
                context.ProposedFixes.Add(fix);
            }
        }
    }

    private static CatalogueProposedFix? CreateFix(CatalogueRunId runId, CatalogueException exception)
    {
        return exception.Type switch
        {
            CatalogueExceptionType.MissingProduct => Actionable(
                runId,
                exception,
                CatalogueProposedActionType.CreateProduct,
                new Dictionary<string, string>
                {
                    ["normalizedName"] = exception.Description,
                    ["offerId"] = exception.CommercialKeyRoot?.OfferId.Value ?? string.Empty,
                    ["skuId"] = exception.CommercialKeyRoot?.SkuId.Value ?? string.Empty
                },
                "Propose creating a Stripe product with mapping metadata for operator approval."),

            CatalogueExceptionType.MissingPrice => Actionable(
                runId,
                exception,
                CatalogueProposedActionType.CreatePrice,
                new Dictionary<string, string>
                {
                    ["unitAmount"] = exception.ExpectedValue ?? string.Empty,
                    ["interval"] = exception.CommercialKey?.Frequency.ToString() ?? string.Empty
                },
                "Propose creating a Stripe price with intended RRP for operator approval."),

            CatalogueExceptionType.IncorrectPrice => Actionable(
                runId,
                exception,
                CatalogueProposedActionType.CreateReplacementPrice,
                new Dictionary<string, string>
                {
                    ["newUnitAmount"] = exception.ExpectedValue ?? string.Empty,
                    ["incorrectPriceId"] = exception.AffectedStripePriceIds.FirstOrDefault().Value ?? string.Empty
                },
                // Stripe prices are immutable by amount — create a replacement price instead of editing in place.
                "Propose creating a new Stripe price with correct RRP; retire use of the incorrect price."),

            CatalogueExceptionType.DuplicateProduct or CatalogueExceptionType.DuplicatePrice =>
                ManualCleanup(runId, exception),

            _ => null
        };
    }

    private static CatalogueProposedFix Actionable(
        CatalogueRunId runId,
        CatalogueException exception,
        CatalogueProposedActionType actionType,
        IReadOnlyDictionary<string, string> proposedState,
        string rationale)
    {
        var key = BuildIdempotencyKey(runId, exception, actionType);
        return new CatalogueProposedFix(
            CatalogueProposedFixId.New(),
            exception.Id,
            actionType,
            key,
            exception.CommercialKeyRoot,
            exception.CommercialKey,
            new Dictionary<string, string> { ["actual"] = exception.ActualValue ?? string.Empty },
            proposedState,
            rationale,
            IsActionable: true);
    }

    private static CatalogueProposedFix ManualCleanup(CatalogueRunId runId, CatalogueException exception)
    {
        var key = BuildIdempotencyKey(runId, exception, CatalogueProposedActionType.FlagManualCleanup);
        return new CatalogueProposedFix(
            CatalogueProposedFixId.New(),
            exception.Id,
            CatalogueProposedActionType.FlagManualCleanup,
            key,
            exception.CommercialKeyRoot,
            exception.CommercialKey,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            "Flag duplicate or conflicting catalogue entries for manual cleanup — no automatic merge or delete.",
            IsActionable: false);
    }

    private static IdempotencyKey BuildIdempotencyKey(
        CatalogueRunId runId,
        CatalogueException exception,
        CatalogueProposedActionType actionType)
    {
        var root = exception.CommercialKeyRoot is { } r
            ? $"{r.OfferId.Value}/{r.SkuId.Value}"
            : "unscoped";
        var frequency = exception.CommercialKey?.Frequency.ToString() ?? string.Empty;
        return new IdempotencyKey($"catalogue:{runId.Value:D}:{root}:{actionType}:{frequency}");
    }
}
