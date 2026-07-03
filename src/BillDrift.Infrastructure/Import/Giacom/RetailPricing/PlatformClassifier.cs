using BillDrift.Domain.Common;

namespace BillDrift.Infrastructure.Import.Giacom.RetailPricing;

/// <summary>Classifies NCE vs Legacy platform values from price list exports.</summary>
internal static class PlatformClassifier
{
    public static PricingPlatform Classify(string? raw, out bool unrecognised)
    {
        unrecognised = false;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return PricingPlatform.Unknown;
        }

        var text = raw.Trim();
        if (IsNce(text))
        {
            return PricingPlatform.Nce;
        }

        if (IsLegacy(text))
        {
            return PricingPlatform.Legacy;
        }

        unrecognised = true;
        return PricingPlatform.Unknown;
    }

    public static bool IsRecognised(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var text = raw.Trim();
        return IsNce(text) || IsLegacy(text);
    }

    private static bool IsNce(string text) =>
        text.Contains("NCE", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("New Commerce", StringComparison.OrdinalIgnoreCase);

    private static bool IsLegacy(string text) =>
        text.Contains("Legacy", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Old Commerce", StringComparison.OrdinalIgnoreCase);
}
