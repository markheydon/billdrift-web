using BillDrift.Domain.Common;

namespace BillDrift.Infrastructure.Import.Giacom.RetailPricing;

/// <summary>Parses term and billing frequency text from reseller price list exports.</summary>
internal static class TermFrequencyParser
{
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
}
