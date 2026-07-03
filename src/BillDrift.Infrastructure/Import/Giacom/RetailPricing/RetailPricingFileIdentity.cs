using System.Security.Cryptography;

namespace BillDrift.Infrastructure.Import.Giacom.RetailPricing;

/// <summary>Computes stable source document identity for reseller price list uploads.</summary>
internal static class RetailPricingFileIdentity
{
    /// <summary>Returns lowercase hex SHA-256 of raw CSV bytes.</summary>
    public static string ComputeSourceDocumentId(ReadOnlySpan<byte> content)
    {
        var hash = SHA256.HashData(content);
        return Convert.ToHexStringLower(hash);
    }
}
