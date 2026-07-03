using System.Security.Cryptography;

namespace BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement;

internal static class SubscriptionManagementFileIdentity
{
    public static string ComputeSourceDocumentId(ReadOnlySpan<byte> csvBytes)
    {
        var hash = SHA256.HashData(csvBytes);
        return Convert.ToHexStringLower(hash);
    }
}
