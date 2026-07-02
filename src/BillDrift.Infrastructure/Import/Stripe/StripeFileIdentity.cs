using System.Security.Cryptography;
using BillDrift.Application.Import;

namespace BillDrift.Infrastructure.Import.Stripe;

internal static class StripeFileIdentity
{
    public static string ComputeSourceDocumentId(ReadOnlySpan<byte> csvBytes)
    {
        var hash = SHA256.HashData(csvBytes);
        return Convert.ToHexStringLower(hash);
    }

    public static string ComputeBundleId(IReadOnlyList<(StripeCsvFileKind Kind, string Hash)> fileHashes)
    {
        var sorted = fileHashes
            .OrderBy(f => f.Kind)
            .Select(f => $"{(int)f.Kind}:{f.Hash}")
            .ToList();

        var payload = string.Join('|', sorted);
        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
