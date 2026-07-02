using BillDrift.Domain.Classification;

namespace BillDrift.Application.Classification;

/// <summary>
/// Ordered rule chain for reconciliation item classification (CR-0 through CR-FALLBACK).
/// Precedence: CR-0 manual override, CR-1 internal, CR-2 custom/service, CR-3 non-CSP supplier,
/// CR-4 Microsoft CSP, CR-5 conservative default.
/// </summary>
public sealed class ClassificationRuleEngine
{
    /// <summary>
    /// Evaluates the rule chain for a single item.
    /// </summary>
    public ItemClassification Evaluate(
        ClassificationSignals signals,
        ClassificationRuleConfiguration config,
        ClassificationOverride? activeOverride,
        DateTimeOffset classifiedAt)
    {
        // CR-0: Manual override short-circuits all automatic rules.
        if (activeOverride is not null)
        {
            return CreateClassification(
                signals.ItemRef,
                activeOverride.Classification,
                ClassificationSource.ManualOverride,
                "ManualOverride",
                ClassificationConfidence.High,
                activeOverride.Notes,
                classifiedAt,
                activeOverride.OperatorId);
        }

        // CR-1: Internal customer via configured Mex IDs.
        if (config.InternalMexIds.Any(id => id == signals.CustomerMexId))
        {
            return CreateClassification(
                signals.ItemRef,
                ReconciliationItemClassification.Internal,
                ClassificationSource.Automatic,
                $"InternalMexId:{signals.CustomerMexId.Value}",
                ClassificationConfidence.High,
                null,
                classifiedAt,
                null);
        }

        // CR-2: Custom/service independence.
        if (IsCustomService(signals))
        {
            var basis = signals.HasStripeBillingOnly
                ? signals.ProductCategory == ProductCategory.CustomService
                    ? $"CustomService:CategoryRule:{signals.ProductCategory}"
                    : "CustomService:StripeOnly"
                : "CustomService:StripeOnly";

            var confidence = signals.ProductCategory == ProductCategory.CustomService
                ? ClassificationConfidence.High
                : ClassificationConfidence.Medium;

            return CreateClassification(
                signals.ItemRef,
                ReconciliationItemClassification.CustomService,
                ClassificationSource.Automatic,
                basis,
                confidence,
                null,
                classifiedAt,
                null);
        }

        // CR-3: Non-CSP supplier — supplier evidence without subscription truth correlation.
        if (signals.HasSupplierCostEvidence && !signals.InSubscriptionTruth)
        {
            return CreateClassification(
                signals.ItemRef,
                ReconciliationItemClassification.NonCspSupplier,
                ClassificationSource.Automatic,
                "NonCsp:SupplierOnly",
                ClassificationConfidence.High,
                null,
                classifiedAt,
                null);
        }

        // CR-4: Microsoft CSP — offer/SKU with truth or price list and Microsoft 365 category.
        if (signals.HasOfferSku &&
            (signals.InSubscriptionTruth || signals.InIntendedPriceList) &&
            signals.ProductCategory == ProductCategory.Microsoft365)
        {
            var confidence = ResolveCspConfidence(signals);
            var basis = BuildCspRuleBasis(signals);

            return CreateClassification(
                signals.ItemRef,
                ReconciliationItemClassification.MicrosoftCsp,
                ClassificationSource.Automatic,
                basis,
                confidence,
                null,
                classifiedAt,
                null);
        }

        // CR-5 / CR-FALLBACK: Conservative default — prefer manual review over false CSP match.
        var signalSummary = string.Join(
            ",",
            new[]
            {
                signals.HasOfferSku ? "OfferSku" : null,
                signals.InSubscriptionTruth ? "Truth" : null,
                signals.InIntendedPriceList ? "PriceList" : null,
                signals.HasSupplierCostEvidence ? "Supplier" : null,
                signals.HasStripeBillingOnly ? "StripeOnly" : null
            }.Where(s => s is not null));

        return CreateClassification(
            signals.ItemRef,
            ReconciliationItemClassification.NonCspSupplier,
            ClassificationSource.Automatic,
            $"ConservativeDefault:{signalSummary}",
            ClassificationConfidence.Low,
            null,
            classifiedAt,
            null);
    }

    private static bool IsCustomService(ClassificationSignals signals)
    {
        if (signals.HasStripeBillingOnly)
        {
            if (signals.ProductCategory == ProductCategory.CustomService)
            {
                return true;
            }

            if (!signals.HasOfferSku && signals.ProductCategory == ProductCategory.Other)
            {
                return true;
            }
        }

        return false;
    }

    private static ClassificationConfidence ResolveCspConfidence(ClassificationSignals signals)
    {
        if (signals.HasOfferSku && signals.InSubscriptionTruth && signals.InIntendedPriceList)
        {
            return ClassificationConfidence.High;
        }

        if (signals.HasOfferSku && (signals.InSubscriptionTruth || signals.InIntendedPriceList))
        {
            return ClassificationConfidence.Medium;
        }

        return ClassificationConfidence.Low;
    }

    private static string BuildCspRuleBasis(ClassificationSignals signals)
    {
        var parts = new List<string>();
        if (signals.HasOfferSku)
        {
            parts.Add("OfferSku");
        }

        if (signals.InSubscriptionTruth)
        {
            parts.Add("Truth");
        }

        if (signals.InIntendedPriceList)
        {
            parts.Add("PriceList");
        }

        return $"MicrosoftCsp:{string.Join("+", parts)}";
    }

    private static ItemClassification CreateClassification(
        ReconciliationItemRef itemRef,
        ReconciliationItemClassification classification,
        ClassificationSource source,
        string ruleBasis,
        ClassificationConfidence confidence,
        string? overrideNotes,
        DateTimeOffset classifiedAt,
        string? operatorId) =>
        new(
            itemRef,
            classification,
            source,
            ruleBasis,
            confidence,
            overrideNotes,
            classifiedAt,
            operatorId);
}
