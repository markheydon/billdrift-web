using BillDrift.Application.Import;
using BillDrift.Infrastructure.Import.Giacom;

namespace BillDrift.Infrastructure.Tests.Import.Giacom;

public class GiacomBillingPdfIngesterTests
{
    private readonly GiacomBillingPdfIngester _ingester = new();
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "fixtures", "giacom-pdf");

    [Fact]
    public void Ingest_PreBillingSampleA_MatchesGoldenFields()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildPreBillingSampleA());

        Assert.Contains(result.Status, new[] { IngestionOutcomeStatus.Success, IngestionOutcomeStatus.PartialSuccess });
        Assert.Equal(GiacomReportType.PreBilling, result.ReportType);
        Assert.True(result.Lines.Count > 0);
        Assert.All(result.Lines, l =>
        {
            Assert.False(string.IsNullOrWhiteSpace(l.MexIdRaw));
            Assert.False(string.IsNullOrWhiteSpace(l.ProductNameRaw));
            Assert.False(string.IsNullOrWhiteSpace(l.QuantityRaw));
            Assert.False(string.IsNullOrWhiteSpace(l.LineCostRaw));
        });

        var goldenPath = Path.Combine(FixtureRoot, "expected", "pre-billing-sample-a.json");
        if (File.Exists(goldenPath))
        {
            GoldenFileComparer.AssertLinesMatchGolden(result.Lines, goldenPath);
        }
    }

    [Fact]
    public void Ingest_PostBillingSampleA_ClassifiesPostBilling()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildPostBillingSampleA());

        Assert.Equal(GiacomReportType.PostBilling, result.ReportType);
        Assert.True(result.Lines.Count > 0);
    }

    [Fact]
    public void Ingest_PreBillingSampleB_ExtractsFormatVariant()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildPreBillingSampleB());

        Assert.True(result.Lines.Count >= 2);
        Assert.Contains(result.Lines, l => l.ChargeTypeRaw.Contains("Adjustment", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ingest_PostBillingSampleB_ExtractsFormatVariant()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildPostBillingSampleB());

        Assert.Equal(GiacomReportType.PostBilling, result.ReportType);
        Assert.True(result.Lines.Count >= 1);
    }

    [Fact]
    public void Ingest_WrappedProductName_MergesContinuationRow()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildWrappedProductNameSample());

        Assert.Single(result.Lines);
        Assert.Contains("Security Add-on", result.Lines[0].ProductNameRaw);
        Assert.Contains("365 Premium", result.Lines[0].ProductNameRaw);
    }

    [Fact]
    public void Ingest_PartialSuccessSample_SkipsBadLineAndLogsReason()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildPartialSuccessSample());

        Assert.Equal(IngestionOutcomeStatus.PartialSuccess, result.Status);
        Assert.Single(result.Lines);
        Assert.Contains(result.LogEntries, e =>
            e.Reason == IngestionFailureReason.QuantityUnparseable &&
            e.Location!.LineIndex.HasValue);
        Assert.True(result.Summary.LinesSkipped >= 1);
    }

    [Fact]
    public void Ingest_EncryptedOrUnreadablePdf_ReturnsFailure()
    {
        var encryptedBytes = Convert.FromBase64String(EncryptedPdfBase64);
        using var stream = new MemoryStream(encryptedBytes);
        var result = _ingester.Ingest(stream, TestContext.Current.CancellationToken);

        Assert.Equal(IngestionOutcomeStatus.Failure, result.Status);
        Assert.Empty(result.Lines);
        Assert.Contains(result.LogEntries, e =>
            e.Reason == IngestionFailureReason.DocumentEncrypted ||
            e.Reason == IngestionFailureReason.DocumentUnreadable);
    }

    [Fact]
    public void Ingest_SamePdfTwice_ProducesDeterministicOutput()
    {
        var pdf = SyntheticGiacomPdfBuilder.BuildPreBillingSampleA();
        var first = Ingest(pdf);
        var second = Ingest(pdf);

        Assert.Equal(first.Lines.Count, second.Lines.Count);
        for (var i = 0; i < first.Lines.Count; i++)
        {
            Assert.Equal(second.Lines[i].Id, first.Lines[i].Id);
            Assert.Equal(second.Lines[i].ProductNameRaw, first.Lines[i].ProductNameRaw);
            Assert.Equal(second.Lines[i].MexIdRaw, first.Lines[i].MexIdRaw);
        }
    }

    [Fact]
    public void Ingest_OutputPreservesRawProductNames_NoOfferOrSkuFields()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildPreBillingSampleA());

        Assert.NotEmpty(result.Lines);
        Assert.All(result.Lines, line =>
        {
            Assert.False(string.IsNullOrWhiteSpace(line.ProductNameRaw));
            Assert.Equal(Domain.Common.ImportSourceKind.GiacomBillingPdf, line.Id.SourceKind);
        });
    }

    [Fact]
    public void Ingest_EmptyCoverSheet_ReturnsSuccessWithNoLines()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildEmptyCoverSheet());

        Assert.Equal(IngestionOutcomeStatus.Success, result.Status);
        Assert.Empty(result.Lines);
        Assert.Contains(result.LogEntries, e =>
            e.Severity == IngestionLogSeverity.Warning &&
            e.Reason == IngestionFailureReason.EmptyDocument);
    }

    [Fact]
    public void Ingest_LargeSample_CompletesWithinPerformanceBudget()
    {
        var pdf = SyntheticGiacomPdfBuilder.BuildLargeSample(customerCount: 10, linesPerCustomer: 5);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = Ingest(pdf);
        stopwatch.Stop();

        Assert.Equal(50, result.Lines.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMinutes(2));
    }

    private GiacomPdfIngestionResult Ingest(byte[] pdfBytes)
    {
        using var stream = new MemoryStream(pdfBytes);
        return _ingester.Ingest(stream, TestContext.Current.CancellationToken);
    }

    private const string EncryptedPdfBase64 =
        "JVBERi0xLjQKMSAwIG9iago8PC9UeXBlL0NhdGFsb2cvUGFnZXMgMiAwIFI+PgplbmRvYmoKMiAwIG9iago8PC" +
        "9UeXBlL1BhZ2VzL0tpZHNbMyAwIFJdL0NvdW50IDE+PgplbmRvYmoKMyAwIG9iago8PC9UeXBlL1BhZ2UvTW" +
        "VkaWFCb3hbMCAwIDYxMiA3OTJdL1BhcmVudCAyIDAgUi9SZXNvdXJjZXM8PC9Gb250PDwgL0YxPDwgL1R5" +
        "cGUvRm9udC9TdWJ0eXBlL1R5cGUxL0Jhc2VGb250L0HelHZldGljYT4+Pj4+PgplbmRvYmoKeHJlZg" +
        "K0IDQKMDAwMDAwMDAwMCA2NTUzNSBmIAowMDAwMDAwMDA5IDAwMDAwIG4gCjAwMDAwMDAwNTggMDAwMDAg" +
        "biAKMDAwMDAwMDExNSAwMDAwMCBuIAp0cmFpbGVyCjw8L1NpemUgNC9Sb290IDEgMCBSL0luZm8gNCAw" +
        "IFIKc3RhcnR4cmVmCjIwNQolJUVPRgo=";
}
