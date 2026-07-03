using System.Globalization;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;

namespace BillDrift.Application.Normalization;

/// <summary>
/// Transforms raw price list and manual override entries into <see cref="IntendedPrice"/> records.
/// </summary>
public sealed class PriceListNormalizer : IPriceListNormalizer
{
    /// <inheritdoc />
    public IntendedPrice Normalize(RawPriceListRow raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var key = BuildCommercialKey(raw.OfferIdRaw, raw.SkuIdRaw, raw.TermRaw, raw.FrequencyRaw, raw.Id);
        var wholesale = ParseRequiredMoney(raw.WholesaleRaw, nameof(raw.WholesaleRaw), raw.Id);
        var rrp = ParseRequiredMoney(raw.RrpRaw, nameof(raw.RrpRaw), raw.Id);

        Money? margin = null;
        if (!string.IsNullOrWhiteSpace(raw.MarginRaw) &&
            SubscriptionManagementNormalizer.TryParseMoney(raw.MarginRaw, out var parsedMargin))
        {
            margin = parsedMargin;
        }

        decimal? marginPercent = null;
        if (!string.IsNullOrWhiteSpace(raw.MarginPercentRaw) &&
            decimal.TryParse(
                raw.MarginPercentRaw.Trim().TrimEnd('%'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsedPercent))
        {
            marginPercent = parsedPercent;
        }

        return new IntendedPrice(
            IntendedPriceId.New(),
            key,
            wholesale,
            rrp,
            margin,
            marginPercent,
            MapStatus(raw.StatusRaw),
            PriceSource.Catalogue,
            SourceReference.FromRawImportId(raw.Id),
            MapPlatform(raw.PlatformRaw),
            ProductClassification.Csp);
    }

    /// <inheritdoc />
    public IntendedPrice Normalize(RawManualPriceEntry raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var key = BuildCommercialKey(
            raw.OfferIdRaw ?? string.Empty,
            raw.SkuIdRaw ?? string.Empty,
            raw.TermRaw,
            raw.FrequencyRaw,
            raw.Id);

        var rrp = ParseRequiredMoney(raw.RrpRaw, nameof(raw.RrpRaw), raw.Id);
        Money wholesale;
        if (!string.IsNullOrWhiteSpace(raw.WholesaleRaw) &&
            SubscriptionManagementNormalizer.TryParseMoney(raw.WholesaleRaw, out var parsedWholesale))
        {
            wholesale = parsedWholesale;
        }
        else
        {
            wholesale = rrp;
        }

        return new IntendedPrice(
            IntendedPriceId.New(),
            key,
            wholesale,
            rrp,
            null,
            null,
            PriceListStatus.Active,
            PriceSource.ManualOverride,
            SourceReference.FromRawImportId(raw.Id),
            PricingPlatform.Unknown,
            ProductClassification.NonCsp);
    }

    /// <summary>Maps raw term text to a <see cref="Term"/> value.</summary>
    public static bool TryParseTerm(string? raw, out Term term)
    {
        term = Term.Unknown;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        term = raw.Trim().ToUpperInvariant() switch
        {
            "MONTHLY" or "P1M" or "1 MONTH" => Term.Monthly,
            "ANNUAL" or "P1Y" or "1 YEAR" or "YEARLY" => Term.Annual,
            "TRIENNIAL" or "P3Y" or "3 YEAR" or "36 MONTH" => Term.Triennial,
            _ => Term.Unknown
        };

        return term != Term.Unknown;
    }

    /// <summary>Maps raw frequency text to a <see cref="BillingFrequency"/> value.</summary>
    public static bool TryParseFrequency(string? raw, out BillingFrequency frequency)
    {
        frequency = BillingFrequency.Unknown;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        frequency = raw.Trim().ToLowerInvariant() switch
        {
            "monthly" or "month" => BillingFrequency.Monthly,
            "annual" or "annually" or "yearly" or "year" => BillingFrequency.Annual,
            _ => BillingFrequency.Unknown
        };

        return frequency != BillingFrequency.Unknown;
    }

    private static CommercialKey BuildCommercialKey(
        string offerIdRaw,
        string skuIdRaw,
        string termRaw,
        string frequencyRaw,
        RawImportId rawId)
    {
        if (string.IsNullOrWhiteSpace(offerIdRaw) || string.IsNullOrWhiteSpace(skuIdRaw))
        {
            throw new NormalizationException(
                rawId,
                nameof(offerIdRaw),
                $"{offerIdRaw}/{skuIdRaw}",
                "Commercial key requires both offer ID and SKU ID.");
        }

        if (!TryParseTerm(termRaw, out var term))
        {
            throw new NormalizationException(rawId, nameof(termRaw), termRaw, "Term could not be mapped.");
        }

        if (!TryParseFrequency(frequencyRaw, out var frequency))
        {
            throw new NormalizationException(
                rawId,
                nameof(frequencyRaw),
                frequencyRaw,
                "Billing frequency could not be mapped.");
        }

        return CommercialKey.Create(
            OfferId.Create(offerIdRaw.Trim()),
            SkuId.Create(skuIdRaw.Trim()),
            term,
            frequency);
    }

    private static Money ParseRequiredMoney(string raw, string fieldName, RawImportId id)
    {
        if (!SubscriptionManagementNormalizer.TryParseMoney(raw, out var money))
        {
            throw new NormalizationException(id, fieldName, raw, "Monetary value could not be parsed.");
        }

        return money;
    }

    private static PriceListStatus MapStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return PriceListStatus.Active;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "active" or "available" => PriceListStatus.Active,
            "end of sale" or "endofsale" or "eos" or "discontinued" => PriceListStatus.EndOfSale,
            _ => PriceListStatus.Unknown
        };
    }

    private static PricingPlatform MapPlatform(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return PricingPlatform.Unknown;
        }

        var text = raw.Trim();
        if (text.Contains("NCE", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("New Commerce", StringComparison.OrdinalIgnoreCase))
        {
            return PricingPlatform.Nce;
        }

        if (text.Contains("Legacy", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Old Commerce", StringComparison.OrdinalIgnoreCase))
        {
            return PricingPlatform.Legacy;
        }

        return PricingPlatform.Unknown;
    }
}
