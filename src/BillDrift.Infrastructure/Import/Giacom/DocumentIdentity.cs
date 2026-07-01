using System.Security.Cryptography;

namespace BillDrift.Infrastructure.Import.Giacom;

public static class DocumentIdentity
{
    public static string ComputeSourceDocumentId(ReadOnlySpan<byte> pdfBytes)
    {
        var hash = SHA256.HashData(pdfBytes);
        return Convert.ToHexStringLower(hash);
    }
}
