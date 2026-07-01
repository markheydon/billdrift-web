using System.Security.Cryptography;

namespace BillDrift.Infrastructure.Import.Giacom;

internal static class DocumentIdentity
{
    public static string ComputeSourceDocumentId(ReadOnlySpan<byte> pdfBytes)
    {
        // SHA-256 over raw PDF bytes yields a stable, deterministic idempotency key for re-import.
        var hash = SHA256.HashData(pdfBytes);
        return Convert.ToHexStringLower(hash);
    }
}
