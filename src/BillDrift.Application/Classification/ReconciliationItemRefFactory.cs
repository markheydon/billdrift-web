using System.Security.Cryptography;
using System.Text;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Classification;

/// <summary>
/// Derives stable business keys for reconciliation items per research R2.
/// </summary>
public static class ReconciliationItemRefFactory
{
    /// <summary>
    /// Enumerates all in-scope item references from reconciliation inputs.
    /// </summary>
    public static IReadOnlyList<ReconciliationItemRef> ExtractAll(ReconciliationInputs inputs, BillingPeriod scope)
    {
        var refs = new List<ReconciliationItemRef>();

        foreach (var line in inputs.SupplierCostLines ?? [])
        {
            if (IsInScope(line.Period, scope))
            {
                refs.Add(FromSupplierCostLine(line));
            }
        }

        foreach (var line in inputs.SubscriptionLines ?? [])
        {
            refs.Add(FromSubscriptionLine(line));
        }

        foreach (var item in inputs.StripeItems ?? [])
        {
            refs.Add(FromStripeBillingItem(item));
        }

        return refs;
    }

    /// <summary>Creates a reference from a supplier cost line.</summary>
    public static ReconciliationItemRef FromSupplierCostLine(SupplierCostLine line)
    {
        var mexId = line.Customer.MexId.Value;
        var reference = line.SupplierReferences.FirstOrDefault().Value;
        var suffix = !string.IsNullOrWhiteSpace(reference)
            ? reference.Trim()
            : HashFallback($"{line.ProductName}:{line.Period.Start:yyyy-MM-dd}");

        var stableKey = $"{mexId}:supplier:{suffix}";
        return ReconciliationItemRef.Create(
            ReconciliationItemKind.SupplierCost,
            stableKey,
            line.Customer.MexId,
            line.Id.Value);
    }

    /// <summary>Creates a reference from a subscription truth line.</summary>
    public static ReconciliationItemRef FromSubscriptionLine(MicrosoftSubscriptionLine line)
    {
        var mexId = line.Customer.MexId.Value;
        var offerId = line.CommercialKeyRoot.OfferId.Value;
        var skuId = line.CommercialKeyRoot.SkuId.Value;
        var correlation = line.SupplierSubscriptionId?.Value
            ?? line.Customer.TenantId?.Value
            ?? "unknown";

        var stableKey = $"{mexId}:truth:{offerId}:{skuId}:{correlation}";
        return ReconciliationItemRef.Create(
            ReconciliationItemKind.SubscriptionTruth,
            stableKey,
            line.Customer.MexId,
            line.Id.Value);
    }

    /// <summary>Creates a reference from a Stripe billing item.</summary>
    public static ReconciliationItemRef FromStripeBillingItem(StripeBillingItem item)
    {
        var stableKey = $"{item.Customer.MexId.Value}:stripe:{item.SubscriptionItemId.Value}";
        return ReconciliationItemRef.Create(
            ReconciliationItemKind.StripeBilling,
            stableKey,
            item.Customer.MexId,
            item.Id.Value);
    }

    private static bool IsInScope(BillingPeriod linePeriod, BillingPeriod scope) =>
        linePeriod.Start <= scope.End && linePeriod.End >= scope.Start;

    private static string HashFallback(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }
}
