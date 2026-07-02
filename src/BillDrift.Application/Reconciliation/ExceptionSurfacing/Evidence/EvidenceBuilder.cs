using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation.ExceptionSurfacing.Evidence;

/// <summary>
/// Builds labelled evidence bundles from match group attachments and mismatch fields (research R6).
/// </summary>
public sealed class EvidenceBuilder
{
    /// <summary>
    /// Builds evidence for a surfaced exception from its backing mismatch and match group.
    /// </summary>
    public IReadOnlyList<ExceptionEvidence> Build(
        Mismatch mismatch,
        EntityMatchGroup? group,
        SurfacingContext context)
    {
        var evidence = new List<ExceptionEvidence>();

        if (group?.SubscriptionLine is { } truth)
        {
            evidence.Add(new ExceptionEvidence(
                EvidenceSource.SubscriptionTruth,
                "Licence Count",
                truth.LicenceCount.ToString(),
                truth.Id.Value.ToString()));

            evidence.Add(new ExceptionEvidence(
                EvidenceSource.SubscriptionTruth,
                "Billing Frequency",
                truth.Frequency.ToString(),
                truth.Id.Value.ToString()));
        }

        if (group?.StripeItem is { } stripe)
        {
            evidence.Add(new ExceptionEvidence(
                EvidenceSource.StripeSubscriptionItem,
                "Quantity",
                stripe.Quantity.ToString(),
                stripe.SubscriptionItemId.Value));

            evidence.Add(new ExceptionEvidence(
                EvidenceSource.StripeSubscriptionItem,
                "Unit Amount",
                FormatMoney(stripe.UnitAmount),
                stripe.SubscriptionItemId.Value));

            evidence.Add(new ExceptionEvidence(
                EvidenceSource.StripeSubscriptionItem,
                "Billing Frequency",
                stripe.Frequency.ToString(),
                stripe.SubscriptionItemId.Value));
        }

        if (group?.SupplierCostLine is { } supplier)
        {
            evidence.Add(new ExceptionEvidence(
                EvidenceSource.SupplierCostLine,
                "Product Name",
                supplier.ProductName,
                supplier.Id.Value.ToString()));

            evidence.Add(new ExceptionEvidence(
                EvidenceSource.SupplierCostLine,
                "Quantity",
                supplier.Quantity.ToString(),
                supplier.Id.Value.ToString()));
        }

        if (group?.IntendedPrice is { } intended)
        {
            evidence.Add(new ExceptionEvidence(
                EvidenceSource.IntendedRetailPrice,
                "Unit Amount (RRP)",
                FormatMoney(intended.Rrp),
                intended.Id.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(mismatch.ExpectedValue))
        {
            evidence.Add(new ExceptionEvidence(
                ResolveExpectedSource(mismatch),
                "Expected",
                mismatch.ExpectedValue));
        }

        if (!string.IsNullOrWhiteSpace(mismatch.ActualValue))
        {
            evidence.Add(new ExceptionEvidence(
                ResolveActualSource(mismatch),
                "Actual",
                mismatch.ActualValue));
        }

        if (mismatch.Type is MismatchType.MappingAmbiguous && !string.IsNullOrWhiteSpace(mismatch.Description))
        {
            var candidates = ExtractCandidates(mismatch.Description);
            foreach (var candidate in candidates)
            {
                evidence.Add(new ExceptionEvidence(
                    EvidenceSource.ProductMapping,
                    "Candidate",
                    candidate));
            }
        }

        if (mismatch.CommercialKey is { } key)
        {
            var cataloguePrice = context.CatalogueIndex.FindPriceForKey(key);
            if (cataloguePrice is not null)
            {
                evidence.Add(new ExceptionEvidence(
                    EvidenceSource.StripeCatalogue,
                    "Catalogue Unit Amount",
                    FormatMoney(cataloguePrice.UnitAmount),
                    cataloguePrice.PriceId.Value));
            }
        }

        return Deduplicate(evidence);
    }

    /// <summary>
    /// Builds evidence for a derived exception without a backing mismatch.
    /// </summary>
    public IReadOnlyList<ExceptionEvidence> BuildForDerived(
        SurfacedException exception,
        SurfacingContext context)
    {
        var evidence = new List<ExceptionEvidence>(exception.Evidence);

        if (exception.Category == ExceptionCategory.OrphanedBillingItem)
        {
            var itemId = exception.Id.Value.Split(':').LastOrDefault();
            var item = context.Run.Inputs.StripeItems.FirstOrDefault(i =>
                i.SubscriptionItemId.Value == itemId);

            if (item is not null)
            {
                evidence.Add(new ExceptionEvidence(
                    EvidenceSource.StripeSubscriptionItem,
                    "Quantity",
                    item.Quantity.ToString(),
                    item.SubscriptionItemId.Value));

                evidence.Add(new ExceptionEvidence(
                    EvidenceSource.StripeSubscriptionItem,
                    "Unit Amount",
                    FormatMoney(item.UnitAmount),
                    item.SubscriptionItemId.Value));
            }
        }

        return Deduplicate(evidence);
    }

    private static EvidenceSource ResolveExpectedSource(Mismatch mismatch) =>
        mismatch.Type switch
        {
            MismatchType.QuantityMismatch or MismatchType.BillingFrequencyMismatch or MismatchType.PriceMismatch
                or MismatchType.MissingInStripe => EvidenceSource.SubscriptionTruth,
            MismatchType.CatalogueMissing => EvidenceSource.IntendedRetailPrice,
            _ => EvidenceSource.ProductMapping
        };

    private static EvidenceSource ResolveActualSource(Mismatch mismatch) =>
        mismatch.Type switch
        {
            MismatchType.QuantityMismatch or MismatchType.BillingFrequencyMismatch or MismatchType.PriceMismatch
                or MismatchType.MissingInStripe => EvidenceSource.StripeSubscriptionItem,
            MismatchType.CatalogueMissing => EvidenceSource.StripeCatalogue,
            _ => EvidenceSource.ProductMapping
        };

    private static IEnumerable<string> ExtractCandidates(string description)
    {
        const string prefix = "Candidates:";
        var idx = description.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            yield break;
        }

        var tail = description[(idx + prefix.Length)..];
        foreach (var part in tail.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            yield return part.Trim().TrimEnd('.');
        }
    }

    private static string FormatMoney(Money money) => $"{money.Amount:F2} {money.Currency.Value}";

    private static List<ExceptionEvidence> Deduplicate(List<ExceptionEvidence> items) =>
        items
            .GroupBy(e => (e.Source, e.Field, e.Value))
            .Select(g => g.First())
            .ToList();
}
