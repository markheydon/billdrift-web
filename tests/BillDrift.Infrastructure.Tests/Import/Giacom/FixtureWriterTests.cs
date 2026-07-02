using BillDrift.Infrastructure.Import.Giacom;

namespace BillDrift.Infrastructure.Tests.Import.Giacom;

public class FixtureWriterTests
{
    [Fact]
    public void GenerateAllFixtures()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GENERATE_FIXTURES")))
        {
            return;
        }

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "giacom-pdf"));
        SyntheticGiacomPdfBuilder.WriteFixturesToDisk(root);

        var ingester = new GiacomBillingPdfIngester();
        WriteGolden(ingester, root, "pre-billing-sample-a.pdf", "expected/pre-billing-sample-a.json");
        WriteGolden(ingester, root, "post-billing-sample-a.pdf", "expected/post-billing-sample-a.json");
        WriteGolden(ingester, root, "pre-billing-sample-b.pdf", "expected/pre-billing-sample-b.json");
        WriteGolden(ingester, root, "post-billing-sample-b.pdf", "expected/post-billing-sample-b.json");

        var encryptedBytes = Convert.FromBase64String(GiacomBillingPdfIngesterTestsEncryptedPdf.Base64);
        File.WriteAllBytes(Path.Combine(root, "encrypted-sample.pdf"), encryptedBytes);
    }

    private static void WriteGolden(GiacomBillingPdfIngester ingester, string root, string pdfName, string goldenRelativePath)
    {
        var pdf = File.ReadAllBytes(Path.Combine(root, pdfName));
        using var stream = new MemoryStream(pdf);
        var result = ingester.Ingest(stream, TestContext.Current.CancellationToken);
        GoldenFileComparer.WriteGoldenFile(result.Lines, Path.Combine(root, goldenRelativePath));
    }
}

internal static class GiacomBillingPdfIngesterTestsEncryptedPdf
{
    internal const string Base64 =
        "JVBERi0xLjQKMSAwIG9iago8PC9UeXBlL0NhdGFsb2cvUGFnZXMgMiAwIFI+PgplbmRvYmoKMiAwIG9iago8PC" +
        "9UeXBlL1BhZ2VzL0tpZHNbMyAwIFJdL0NvdW50IDE+PgplbmRvYmoKMyAwIG9iago8PC9UeXBlL1BhZ2UvTW" +
        "VkaWFCb3hbMCAwIDYxMiA3OTJdL1BhcmVudCAyIDAgUi9SZXNvdXJjZXM8PC9Gb250PDwgL0YxPDwgL1R5" +
        "cGUvRm9udC9TdWJ0eXBlL1R5cGUxL0Jhc2VGb250L0HelHZldGljYT4+Pj4+PgplbmRvYmoKeHJlZg" +
        "K0IDQKMDAwMDAwMDAwMCA2NTUzNSBmIAowMDAwMDAwMDA5IDAwMDAwIG4gCjAwMDAwMDAwNTggMDAwMDAg" +
        "biAKMDAwMDAwMDExNSAwMDAwMCBuIAp0cmFpbGVyCjw8L1NpemUgNC9Sb290IDEgMCBSL0luZm8gNCAw" +
        "IFIKc3RhcnR4cmVmCjIwNQolJUVPRgo=";
}
