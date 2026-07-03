using System.Security.Cryptography;
using System.Text;
using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.History;

/// <summary>Computes deterministic cross-run mismatch identity keys.</summary>
public sealed class StableMismatchKeyFactory
{
    /// <summary>Creates a stable key for the given mismatch.</summary>
    public StableMismatchKey Create(Mismatch mismatch)
    {
        var mexId = mismatch.Customer?.MexId.Value.ToLowerInvariant() ?? "_unknown_";
        var offerId = mismatch.CommercialKey?.OfferId.Value.ToLowerInvariant() ?? "_noid_";
        var skuId = mismatch.CommercialKey?.SkuId.Value.ToLowerInvariant() ?? "_noid_";
        var mismatchType = mismatch.Type.ToString().ToLowerInvariant();
        var distinguisher = GetDistinguisher(mismatch);

        var raw = $"{mexId}|{offerId}/{skuId}|{mismatchType}|{distinguisher}";
        if (raw.Length > 512)
        {
            raw = raw[..512];
        }

        return StableMismatchKey.Create(raw);
    }

    private static string GetDistinguisher(Mismatch mismatch) =>
        mismatch.Type switch
        {
            MismatchType.QuantityMismatch => "qty",
            MismatchType.PriceMismatch => GetPriceDistinguisher(mismatch),
            MismatchType.BillingFrequencyMismatch => GetFrequencyDistinguisher(mismatch),
            MismatchType.MissingInStripe => "missing-stripe",
            MismatchType.CatalogueMissing => GetCatalogueMissingDistinguisher(mismatch),
            MismatchType.MappingMissing => $"mapping:{Hash8(mismatch.Description)}",
            MismatchType.MappingAmbiguous => $"mapping-ambiguous:{Hash8(mismatch.Description)}",
            _ => $"general:{Hash8(mismatch.Description)}"
        };

    private static string GetPriceDistinguisher(Mismatch mismatch)
    {
        var frequency = mismatch.CommercialKey?.Frequency.ToString().ToLowerInvariant() ?? "unknown";
        var amount = NormalizeAmount(mismatch.ExpectedValue);
        return $"{frequency}:{amount}";
    }

    private static string GetFrequencyDistinguisher(Mismatch mismatch) =>
        mismatch.ExpectedValue?.Trim().ToLowerInvariant() ?? "unknown";

    private static string GetCatalogueMissingDistinguisher(Mismatch mismatch)
    {
        var frequency = mismatch.CommercialKey?.Frequency.ToString().ToLowerInvariant() ?? "unknown";
        return $"catalogue-missing:{frequency}";
    }

    private static string NormalizeAmount(string? expectedValue)
    {
        if (string.IsNullOrWhiteSpace(expectedValue))
        {
            return Hash8(expectedValue);
        }

        if (decimal.TryParse(expectedValue, out var amount))
        {
            return ((long)(amount * 100)).ToString();
        }

        return Hash8(expectedValue);
    }

    private static string Hash8(string? value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }
}
