using System.Globalization;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;

namespace BillDrift.Application.Normalization;

/// <summary>
/// Transforms raw Giacom Subscription Management rows into normalized <see cref="MicrosoftSubscriptionLine"/> records.
/// </summary>
public sealed class SubscriptionManagementNormalizer : ISubscriptionManagementNormalizer
{
    /// <inheritdoc />
    public MicrosoftSubscriptionLine Normalize(RawSubscriptionManagementRow raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var mexId = MexId.Create(raw.MexIdRaw.Trim().ToUpperInvariant());
        var displayName = string.IsNullOrWhiteSpace(raw.CustomerNameRaw) ? null : raw.CustomerNameRaw.Trim();
        TenantId? tenantId = string.IsNullOrWhiteSpace(raw.TenantIdRaw) ? null : TenantId.Create(raw.TenantIdRaw.Trim());

        var customer = CustomerIdentity.Create(mexId, displayName, tenantId);

        if (string.IsNullOrWhiteSpace(raw.OfferIdRaw) || string.IsNullOrWhiteSpace(raw.SkuIdRaw))
        {
            throw new NormalizationException(
                raw.Id,
                nameof(raw.OfferIdRaw),
                $"{raw.OfferIdRaw}/{raw.SkuIdRaw}",
                "Commercial key requires both offer ID and SKU ID.");
        }

        var commercialKey = CommercialKeyRoot.Create(
            OfferId.Create(raw.OfferIdRaw.Trim()),
            SkuId.Create(raw.SkuIdRaw.Trim()));

        if (!TryParseLicenceCount(raw.LicencesRaw, out var licenceCount))
        {
            throw new NormalizationException(
                raw.Id,
                nameof(raw.LicencesRaw),
                raw.LicencesRaw,
                "Licence count could not be parsed.");
        }

        var term = MapTerm(raw.TermRaw);
        var frequency = MapFrequency(raw.FrequencyRaw);
        var renewalDate = ParseDate(raw.RenewalDateRaw);
        var status = MapStatus(raw.StatusRaw);

        SupplierSubscriptionId? supplierSubscriptionId = string.IsNullOrWhiteSpace(raw.SupplierSubscriptionIdRaw)
            ? null
            : SupplierSubscriptionId.Create(raw.SupplierSubscriptionIdRaw.Trim());

        var productDisplay = BuildProductDisplay(raw);
        var lifecycle = BuildLifecycle(raw);

        var lineId = MicrosoftSubscriptionLineId.New();

        return new MicrosoftSubscriptionLine(
            lineId,
            customer,
            commercialKey,
            licenceCount,
            term,
            frequency,
            renewalDate,
            status,
            supplierSubscriptionId,
            SourceReference.FromRawImportId(raw.Id),
            productDisplay,
            lifecycle);
    }

    private static ProductDisplayFacts? BuildProductDisplay(RawSubscriptionManagementRow raw)
    {
        if (string.IsNullOrWhiteSpace(raw.ServiceRaw) &&
            string.IsNullOrWhiteSpace(raw.ProductNameRaw) &&
            string.IsNullOrWhiteSpace(raw.ProductTypeRaw))
        {
            return null;
        }

        return new ProductDisplayFacts(
            TrimOrNull(raw.ServiceRaw),
            TrimOrNull(raw.ProductNameRaw),
            TrimOrNull(raw.ProductTypeRaw));
    }

    private static SubscriptionLifecycleFacts? BuildLifecycle(RawSubscriptionManagementRow raw)
    {
        var isNce = ParseBooleanFlag(raw.IsNceRaw);
        var isTrial = ParseBooleanFlag(raw.IsTrialRaw);
        var endOfTermAction = TrimOrNull(raw.EndOfTermActionRaw);
        var cancellableUntil = ParseDate(raw.CancellableUntilRaw);
        var migrationToNce = TrimOrNull(raw.MigrationToNceRaw);
        int? assignedLicences = TryParseOptionalInt(raw.AssignedLicencesRaw, out var assigned) ? assigned : null;
        Money? price = TryParseMoney(raw.PriceRaw, out var parsedPrice) ? parsedPrice : null;
        Money? erp = TryParseMoney(raw.ErpRaw, out var parsedErp) ? parsedErp : null;

        if (isNce is null && isTrial is null && endOfTermAction is null && cancellableUntil is null &&
            migrationToNce is null && assignedLicences is null && price is null && erp is null)
        {
            return null;
        }

        return new SubscriptionLifecycleFacts(
            isNce,
            isTrial,
            endOfTermAction,
            cancellableUntil,
            migrationToNce,
            assignedLicences,
            price,
            erp);
    }

    /// <summary>Attempts to parse a non-negative licence count from raw CSV text.</summary>
    public static bool TryParseLicenceCount(string? raw, out int count)
    {
        count = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count) && count >= 0;
    }

    /// <summary>Attempts to parse a GBP money value from raw CSV text.</summary>
    public static bool TryParseMoney(string? raw, out Money money)
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

    internal static DateOnly? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "dd-MMM-yyyy", "yyyy-MM-dd"];
        var uk = CultureInfo.GetCultureInfo("en-GB");

        if (DateOnly.TryParseExact(text, formats, uk, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return DateOnly.TryParse(text, uk, DateTimeStyles.None, out parsed) ? parsed : null;
    }

    internal static SubscriptionStatus MapStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return SubscriptionStatus.Unknown;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "active" => SubscriptionStatus.Active,
            "suspended" => SubscriptionStatus.Suspended,
            "cancelled" or "canceled" => SubscriptionStatus.Cancelled,
            "pending" => SubscriptionStatus.Pending,
            _ => SubscriptionStatus.Unknown
        };
    }

    internal static Term MapTerm(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Term.Unknown;
        }

        return raw.Trim().ToUpperInvariant() switch
        {
            "MONTHLY" or "P1M" or "1 MONTH" => Term.P1M,
            "ANNUAL" or "P1Y" or "1 YEAR" or "YEARLY" => Term.P1Y,
            _ => Term.Unknown
        };
    }

    internal static BillingFrequency MapFrequency(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return BillingFrequency.Unknown;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "monthly" or "month" => BillingFrequency.Monthly,
            "annual" or "annually" or "yearly" or "year" => BillingFrequency.Annual,
            _ => BillingFrequency.Unknown
        };
    }

    private static bool TryParseOptionalInt(string? raw, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool? ParseBooleanFlag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "y" or "yes" or "true" or "1" => true,
            "n" or "no" or "false" or "0" => false,
            _ => null
        };
    }
}
