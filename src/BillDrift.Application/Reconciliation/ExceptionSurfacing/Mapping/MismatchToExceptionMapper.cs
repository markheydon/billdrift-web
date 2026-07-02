using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation.ExceptionSurfacing.Mapping;

/// <summary>
/// Maps engine <see cref="Mismatch"/> records to operator-facing categories and domains
/// per contracts/mismatch-to-exception-mapping.md.
/// </summary>
public sealed class MismatchToExceptionMapper
{
    private static readonly Dictionary<MismatchSeverity, ExceptionSeverity> SeverityMap = new()
    {
        [MismatchSeverity.Info] = ExceptionSeverity.Info,
        [MismatchSeverity.Warning] = ExceptionSeverity.Warning,
        [MismatchSeverity.Error] = ExceptionSeverity.Error
    };

    /// <summary>
    /// Maps a single mismatch to a surfaced exception candidate.
    /// </summary>
    public SurfacedException Map(Mismatch mismatch, SurfacingContext context)
    {
        var group = context.FindMatchGroup(mismatch);
        var (category, domain) = ResolveCategoryAndDomain(mismatch, group, context);
        var severity = SeverityMap[mismatch.Severity];
        var explanation = BuildExplanation(mismatch, category);
        var product = BuildProductContext(mismatch, group);
        var proposedChangeId = ResolveProposedChangeId(mismatch, category, group, context);

        return new SurfacedException(
            SurfacedExceptionId.FromMismatch(context.Run.Id, mismatch.Id),
            category,
            domain,
            severity,
            mismatch.Customer ?? group?.Customer ?? throw new InvalidOperationException(
                $"Mismatch {mismatch.Id} has no customer identity."),
            product,
            explanation,
            [],
            RequiresActionNow: false,
            proposedChangeId,
            SuppressedSiblingCount: 0,
            [mismatch.Id],
            group?.Id);
    }

    private static (ExceptionCategory Category, ReconciliationDomain Domain) ResolveCategoryAndDomain(
        Mismatch mismatch,
        EntityMatchGroup? group,
        SurfacingContext context)
    {
        return mismatch.Type switch
        {
            MismatchType.MissingInStripe => (ExceptionCategory.MissingBillingItem, ReconciliationDomain.TruthVsStripe),
            MismatchType.QuantityMismatch => (ExceptionCategory.QuantityLicenceMismatch, ReconciliationDomain.TruthVsStripe),
            MismatchType.BillingFrequencyMismatch => (ExceptionCategory.BillingFrequencyMismatch, ReconciliationDomain.TruthVsStripe),
            MismatchType.PriceMismatch => (ExceptionCategory.StripePriceRrpMismatch, ReconciliationDomain.TruthVsStripe),
            MismatchType.MappingAmbiguous => (ExceptionCategory.OfferSkuAmbiguousMapping, ReconciliationDomain.SupplierCostVsMapping),
            MismatchType.MappingMissing => ResolveMappingMissing(mismatch, group, context),
            MismatchType.CatalogueMissing => (SubdivideCatalogueMissing(mismatch, group, context), ReconciliationDomain.PricingVsCatalogue),
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch.Type, "Unknown mismatch type.")
        };
    }

    private static (ExceptionCategory, ReconciliationDomain) ResolveMappingMissing(
        Mismatch mismatch,
        EntityMatchGroup? group,
        SurfacingContext context)
    {
        if (IsNonCsp(mismatch, group, context))
        {
            return (ExceptionCategory.NonCspManualReview, ReconciliationDomain.SupplierCostVsMapping);
        }

        if (HasMexIdConflict(group))
        {
            return (ExceptionCategory.MexIdMismatch, ReconciliationDomain.SupplierCostVsMapping);
        }

        return (ExceptionCategory.OfferSkuAmbiguousMapping, ReconciliationDomain.SupplierCostVsMapping);
    }

    private static bool IsNonCsp(Mismatch mismatch, EntityMatchGroup? group, SurfacingContext context)
    {
        if (mismatch.Description.Contains("Non-CSP", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (group?.SupplierCostLine is not null)
        {
            var mapping = FindMappingForGroup(group, context);
            if (mapping?.Classification == ProductClassification.NonCsp)
            {
                return true;
            }
        }

        if (mismatch.InvolvedEntityIds.SupplierCostLineId is { } lineId)
        {
            var line = context.Run.Inputs.SupplierCostLines.FirstOrDefault(l => l.Id == lineId);
            if (line is not null)
            {
                var mapping = context.Run.Inputs.ProductMappings.FirstOrDefault(m =>
                    m.SupplierNameVariants.Any(v =>
                        string.Equals(v.DisplayName, line.ProductName, StringComparison.OrdinalIgnoreCase)));
                if (mapping?.Classification == ProductClassification.NonCsp)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasMexIdConflict(EntityMatchGroup? group)
    {
        if (group is null)
        {
            return false;
        }

        var mexIds = new HashSet<string>(StringComparer.Ordinal);
        if (group.Customer.MexId.Value is { } customerMex)
        {
            mexIds.Add(customerMex);
        }

        if (group.SubscriptionLine?.Customer.MexId.Value is { } subMex)
        {
            if (mexIds.Count > 0 && !mexIds.Contains(subMex))
            {
                return true;
            }

            mexIds.Add(subMex);
        }

        if (group.StripeItem?.Customer.MexId.Value is { } stripeMex)
        {
            if (mexIds.Count > 0 && !mexIds.Contains(stripeMex))
            {
                return true;
            }
        }

        if (group.SupplierCostLine?.Customer.MexId.Value is { } supplierMex)
        {
            if (mexIds.Count > 0 && !mexIds.Contains(supplierMex))
            {
                return true;
            }
        }

        return false;
    }

    private static ExceptionCategory SubdivideCatalogueMissing(
        Mismatch mismatch,
        EntityMatchGroup? group,
        SurfacingContext context)
    {
        if (mismatch.CommercialKey is not { } key)
        {
            return ExceptionCategory.StripePriceMissing;
        }

        var root = CommercialKeyRoot.Create(key.OfferId, key.SkuId);
        var prices = context.CatalogueIndex.FindPrices(root);
        var hasProduct = prices.Count > 0 ||
                         context.CatalogueIndex.FindItemsByRoot(
                             mismatch.Customer ?? group!.Customer, root).Count > 0;

        if (!hasProduct)
        {
            return ExceptionCategory.StripeProductMissing;
        }

        var price = context.CatalogueIndex.FindPriceForKey(key);
        if (price is null)
        {
            return ExceptionCategory.StripePriceMissing;
        }

        if (group?.IntendedPrice is not null)
        {
            var intended = group.IntendedPrice.Rrp;
            var tolerance = context.Options.PriceTolerance;
            var diff = Math.Abs(price.UnitAmount.Amount - intended.Amount);
            if (diff > tolerance.Amount)
            {
                return ExceptionCategory.StripePriceRrpMismatch;
            }
        }

        return ExceptionCategory.StripePriceMissing;
    }

    private static string BuildExplanation(Mismatch mismatch, ExceptionCategory category)
    {
        if (!string.IsNullOrWhiteSpace(mismatch.Description))
        {
            return mismatch.Description;
        }

        return category switch
        {
            ExceptionCategory.MissingBillingItem =>
                "Subscription should be billed in Stripe but no matching item was found.",
            ExceptionCategory.OrphanedBillingItem =>
                "Stripe is billing an item with no matching active subscription.",
            ExceptionCategory.QuantityLicenceMismatch =>
                "Licence count in Stripe does not match subscription truth.",
            ExceptionCategory.BillingFrequencyMismatch =>
                "Billing frequency in Stripe does not match the subscription term.",
            ExceptionCategory.StripePriceRrpMismatch =>
                "Stripe unit price does not match intended retail price.",
            ExceptionCategory.StripeProductMissing =>
                "No Stripe product exists for this offer/SKU.",
            ExceptionCategory.StripePriceMissing =>
                "Stripe product exists but the required price is missing.",
            ExceptionCategory.OfferSkuAmbiguousMapping =>
                "Cannot uniquely map this line to a product.",
            ExceptionCategory.MexIdMismatch =>
                "Customer Mex ID differs across data sources.",
            ExceptionCategory.NonCspManualReview =>
                "Non-CSP line requires manual mapping and pricing rules.",
            ExceptionCategory.ProductMismatch =>
                "Subscription truth and Stripe reference different products.",
            _ => mismatch.Description
        };
    }

    private static ProductContext? BuildProductContext(Mismatch mismatch, EntityMatchGroup? group)
    {
        var key = mismatch.CommercialKey ?? group?.CommercialKey;
        if (key is null)
        {
            return null;
        }

        var k = key.Value;
        return new ProductContext(
            k,
            $"{k.OfferId.Value}/{k.SkuId.Value} ({k.Term}/{k.Frequency})",
            k.OfferId,
            k.SkuId);
    }

    private static ProposedChangeId? ResolveProposedChangeId(
        Mismatch mismatch,
        ExceptionCategory category,
        EntityMatchGroup? group,
        SurfacingContext context)
    {
        if (category is ExceptionCategory.OfferSkuAmbiguousMapping
            or ExceptionCategory.NonCspManualReview
            or ExceptionCategory.MexIdMismatch)
        {
            return null;
        }

        if (group?.Confidence is MatchConfidence.Low or MatchConfidence.None)
        {
            return null;
        }

        if (!context.ProposedChangeByMismatchId.TryGetValue(mismatch.Id, out var proposed))
        {
            return null;
        }

        var eligible = mismatch.Type switch
        {
            MismatchType.MissingInStripe => proposed.ActionType == ProposedActionType.CreateMissingItem,
            MismatchType.QuantityMismatch => proposed.ActionType == ProposedActionType.UpdateQuantity,
            MismatchType.BillingFrequencyMismatch or MismatchType.PriceMismatch =>
                proposed.ActionType == ProposedActionType.SwitchPrice,
            MismatchType.CatalogueMissing =>
                proposed.ActionType == ProposedActionType.CreateOrUpdateCatalogueEntry &&
                context.Options.ProposeCatalogueChanges,
            _ => false
        };

        return eligible ? proposed.Id : null;
    }

    private static ProductMapping? FindMappingForGroup(EntityMatchGroup group, SurfacingContext context)
    {
        if (group.CommercialKey is not { } key)
        {
            return null;
        }

        return context.Run.Inputs.ProductMappings.FirstOrDefault(m =>
            m.Key.OfferId == key.OfferId &&
            m.Key.SkuId == key.SkuId);
    }
}
