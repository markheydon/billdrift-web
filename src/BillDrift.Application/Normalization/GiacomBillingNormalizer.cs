using System.Globalization;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;

namespace BillDrift.Application.Normalization;

/// <summary>
/// Transforms raw Giacom billing PDF lines into normalized <see cref="SupplierCostLine"/> records.
/// </summary>
public sealed class GiacomBillingNormalizer : IGiacomBillingNormalizer
{
    /// <inheritdoc />
    public SupplierCostLine Normalize(RawGiacomBillingLine raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (string.IsNullOrWhiteSpace(raw.MexIdRaw))
        {
            throw new NormalizationException(raw.Id, nameof(raw.MexIdRaw), raw.MexIdRaw, "MexId is required.");
        }

        var mexId = MexId.Create(raw.MexIdRaw.Trim().ToUpperInvariant());
        var customer = CustomerIdentity.Create(mexId);

        if (!TryParseQuantity(raw.QuantityRaw, out var quantity))
        {
            throw new NormalizationException(raw.Id, nameof(raw.QuantityRaw), raw.QuantityRaw, "Quantity could not be parsed.");
        }

        if (!TryParseMoney(raw.LineCostRaw, out var lineCost))
        {
            throw new NormalizationException(raw.Id, nameof(raw.LineCostRaw), raw.LineCostRaw, "Line cost could not be parsed.");
        }

        var chargeType = MapChargeType(raw.ChargeTypeRaw);
        var period = ParseBillingPeriod(raw.PeriodStartRaw, raw.PeriodEndRaw, raw.Id);
        var productName = string.IsNullOrWhiteSpace(raw.ProductNameRaw) ? "Unknown" : raw.ProductNameRaw.Trim();
        var references = raw.SupplierReferenceIds
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => SupplierReferenceId.Create(r.Trim()))
            .ToList();

        return new SupplierCostLine(
            SupplierCostLineId.New(),
            customer,
            productName,
            quantity,
            chargeType,
            period,
            lineCost,
            references,
            SourceReference.FromRawImportId(raw.Id));
    }

    private static ChargeType MapChargeType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ChargeType.Recurring;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        return normalized.Contains("pro-rated", StringComparison.Ordinal) ||
               normalized.Contains("prorated", StringComparison.Ordinal) ||
               normalized.Contains("pro rated", StringComparison.Ordinal)
            ? ChargeType.ProRatedAdjustment
            : ChargeType.Recurring;
    }

    private static BillingPeriod ParseBillingPeriod(string? startRaw, string? endRaw, RawImportId id)
    {
        var start = SubscriptionManagementNormalizer.ParseDate(startRaw);
        var end = SubscriptionManagementNormalizer.ParseDate(endRaw);

        if (start is null || end is null)
        {
            throw new NormalizationException(
                id,
                nameof(startRaw),
                $"{startRaw}/{endRaw}",
                "Billing period start and end are required.");
        }

        return new BillingPeriod(start.Value, end.Value);
    }

    private static bool TryParseQuantity(string? raw, out int quantity)
    {
        quantity = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity);
    }

    private static bool TryParseMoney(string? raw, out Money money)
    {
        money = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = raw.Trim()
            .Replace("£", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) &&
            !decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.GetCultureInfo("en-GB"), out amount))
        {
            return false;
        }

        money = Money.Gbp(amount);
        return true;
    }
}
