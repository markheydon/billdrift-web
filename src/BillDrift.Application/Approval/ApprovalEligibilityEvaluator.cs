using BillDrift.Application.Classification;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Approval;

/// <summary>Evaluates whether a proposal may be approved for bill-impacting export.</summary>
public sealed class ApprovalEligibilityEvaluator
{
    private static readonly HashSet<ExceptionCategory> InvestigationCategories =
    [
        ExceptionCategory.OfferSkuAmbiguousMapping,
        ExceptionCategory.NonCspManualReview,
        ExceptionCategory.StripeProductMissing,
        ExceptionCategory.StripePriceMissing
    ];

    /// <summary>Evaluates eligibility for a proposed change snapshot.</summary>
    public EligibilityEvaluation EvaluateProposedChange(
        ProposedChange proposedChange,
        Mismatch? mismatch,
        EntityMatchGroup? matchGroup,
        SurfacedException? linkedException,
        ClassificationContext? classifications,
        IReadOnlyList<ApprovalProposal> existingProposals)
    {
        if (linkedException is { RequiresActionNow: false })
        {
            return Blocked(ApprovalEligibility.InvestigationOnly, "Linked exception does not require action now.");
        }

        if (linkedException is not null && InvestigationCategories.Contains(linkedException.Category))
        {
            return Blocked(ApprovalEligibility.InvestigationOnly, $"Exception category {linkedException.Category} requires investigation.");
        }

        if (matchGroup is { Confidence: MatchConfidence.Low or MatchConfidence.None })
        {
            return Blocked(ApprovalEligibility.InvestigationOnly, "Match confidence is too low for automatic approval.");
        }

        var classificationBlock = EvaluateClassification(mismatch, classifications);
        if (classificationBlock is not null)
        {
            return classificationBlock;
        }

        if (proposedChange.ActionType == ProposedActionType.CreateOrUpdateCatalogueEntry)
        {
            var conflict = DetectCatalogueConflict(proposedChange, existingProposals);
            if (conflict is not null)
            {
                return conflict;
            }
        }
        else
        {
            var dependency = DetectDependencyBlocked(proposedChange, existingProposals);
            if (dependency is not null)
            {
                return dependency;
            }
        }

        var risk = DetectRiskIndicator(mismatch, proposedChange);
        return new EligibilityEvaluation(ApprovalEligibility.Eligible, null, risk, MapCategory(proposedChange.ActionType));
    }

    /// <summary>Evaluates eligibility for an investigation-only synthesized proposal.</summary>
    public EligibilityEvaluation EvaluateInvestigation(SurfacedException exception) =>
        new(
            ApprovalEligibility.InvestigationOnly,
            exception.Explanation,
            ApprovalRiskIndicator.None,
            ApprovalProposalCategory.Investigation);

    private static EligibilityEvaluation? EvaluateClassification(
        Mismatch? mismatch,
        ClassificationContext? classifications)
    {
        if (classifications is null || mismatch?.Customer is null)
        {
            return null;
        }

        foreach (var group in classifications.ByStableKey.Values)
        {
            if (!string.Equals(
                    group.ItemRef.CustomerMexId.Value,
                    mismatch.Customer.MexId.Value,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (group.Classification is ReconciliationItemClassification.Internal or ReconciliationItemClassification.CustomService)
            {
                return Blocked(
                    ApprovalEligibility.InvestigationOnly,
                    $"Classification {group.Classification} requires manual review.");
            }

            if (group.Classification == ReconciliationItemClassification.NonCspSupplier)
            {
                return Blocked(
                    ApprovalEligibility.InvestigationOnly,
                    "Non-CSP supplier item requires manual mapping review.");
            }
        }

        return null;
    }

    private static EligibilityEvaluation? DetectCatalogueConflict(
        ProposedChange proposedChange,
        IReadOnlyList<ApprovalProposal> existingProposals)
    {
        var root = proposedChange.CataloguePayload?.CommercialKeyRoot;
        if (root is null)
        {
            return null;
        }

        // The proposal under evaluation is not yet part of existingProposals, so any single
        // pending/approved catalogue proposal already targeting the same commercial key means two
        // proposals compete for one catalogue entry. Blocking here prevents conflicting catalogue
        // actions from both reaching the approval/export path.
        var conflictsWithExisting = existingProposals.Any(p =>
            p.Category == ApprovalProposalCategory.Catalogue &&
            p.CommercialKeyRoot is not null &&
            root.Equals(p.CommercialKeyRoot.Value) &&
            p.State is ApprovalDecisionState.Pending or ApprovalDecisionState.Approved);

        if (conflictsWithExisting)
        {
            return Blocked(
                ApprovalEligibility.CatalogueConflict,
                "Duplicate or conflicting catalogue entry detected for the same commercial key.");
        }

        return null;
    }

    private static EligibilityEvaluation? DetectDependencyBlocked(
        ProposedChange proposedChange,
        IReadOnlyList<ApprovalProposal> existingProposals)
    {
        var pendingCatalogue = existingProposals.Any(p =>
            p.Category == ApprovalProposalCategory.Catalogue &&
            p.State == ApprovalDecisionState.Pending &&
            p.Eligibility == ApprovalEligibility.Eligible);

        if (pendingCatalogue && proposedChange.ActionType != ProposedActionType.CreateOrUpdateCatalogueEntry)
        {
            return Blocked(
                ApprovalEligibility.DependencyBlocked,
                "Approve prerequisite catalogue proposals before subscription changes.");
        }

        return null;
    }

    private static ApprovalRiskIndicator? DetectRiskIndicator(Mismatch? mismatch, ProposedChange proposedChange)
    {
        if (mismatch is null)
        {
            return ApprovalRiskIndicator.None;
        }

        if (mismatch.Type is MismatchType.QuantityMismatch or MismatchType.PriceMismatch)
        {
            if (TryParseDecimal(mismatch.ExpectedValue, out var expected) &&
                TryParseDecimal(mismatch.ActualValue, out var actual) &&
                actual < expected)
            {
                return ApprovalRiskIndicator.RevenueReduction;
            }
        }

        if (proposedChange.ActionType == ProposedActionType.CreateOrUpdateCatalogueEntry)
        {
            return ApprovalRiskIndicator.CatalogueWideImpact;
        }

        return ApprovalRiskIndicator.None;
    }

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        result = 0;
        return value is not null && decimal.TryParse(value, out result);
    }

    private static ApprovalProposalCategory MapCategory(ProposedActionType actionType) =>
        actionType == ProposedActionType.CreateOrUpdateCatalogueEntry
            ? ApprovalProposalCategory.Catalogue
            : ApprovalProposalCategory.Subscription;

    private static EligibilityEvaluation Blocked(ApprovalEligibility eligibility, string reason) =>
        new(eligibility, reason, ApprovalRiskIndicator.None, ApprovalProposalCategory.Investigation);
}

/// <summary>Result of eligibility evaluation for a proposal snapshot.</summary>
public sealed record EligibilityEvaluation(
    ApprovalEligibility Eligibility,
    string? Reason,
    ApprovalRiskIndicator? RiskIndicator,
    ApprovalProposalCategory Category);
