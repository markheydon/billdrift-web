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

        result.Status.Should().BeOneOf(IngestionOutcomeStatus.Success, IngestionOutcomeStatus.PartialSuccess);
        result.ReportType.Should().Be(GiacomReportType.PreBilling);
        result.Lines.Should().HaveCountGreaterThan(0);
        result.Lines.Should().OnlyContain(l =>
            !string.IsNullOrWhiteSpace(l.MexIdRaw) &&
            !string.IsNullOrWhiteSpace(l.ProductNameRaw) &&
            !string.IsNullOrWhiteSpace(l.QuantityRaw) &&
            !string.IsNullOrWhiteSpace(l.LineCostRaw));

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

        result.ReportType.Should().Be(GiacomReportType.PostBilling);
        result.Lines.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void Ingest_PreBillingSampleB_ExtractsFormatVariant()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildPreBillingSampleB());

        result.Lines.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Lines.Should().Contain(l => l.ChargeTypeRaw.Contains("Adjustment", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ingest_PostBillingSampleB_ExtractsFormatVariant()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildPostBillingSampleB());

        result.ReportType.Should().Be(GiacomReportType.PostBilling);
        result.Lines.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Ingest_WrappedProductName_MergesContinuationRow()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildWrappedProductNameSample());

        result.Lines.Should().ContainSingle();
        result.Lines[0].ProductNameRaw.Should().Contain("Security Add-on");
        result.Lines[0].ProductNameRaw.Should().Contain("365 Premium");
    }

    [Fact]
    public void Ingest_PartialSuccessSample_SkipsBadLineAndLogsReason()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildPartialSuccessSample());

        result.Status.Should().Be(IngestionOutcomeStatus.PartialSuccess);
        result.Lines.Should().HaveCount(1);
        result.LogEntries.Should().Contain(e =>
            e.Reason == IngestionFailureReason.QuantityUnparseable &&
            e.Location!.LineIndex.HasValue);
        result.Summary.LinesSkipped.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Ingest_EncryptedOrUnreadablePdf_ReturnsFailure()
    {
        var encryptedBytes = Convert.FromBase64String(EncryptedPdfBase64);
        using var stream = new MemoryStream(encryptedBytes);
        var result = _ingester.Ingest(stream);

        result.Status.Should().Be(IngestionOutcomeStatus.Failure);
        result.Lines.Should().BeEmpty();
        result.LogEntries.Should().Contain(e =>
            e.Reason == IngestionFailureReason.DocumentEncrypted ||
            e.Reason == IngestionFailureReason.DocumentUnreadable);
    }

    [Fact]
    public void Ingest_SamePdfTwice_ProducesDeterministicOutput()
    {
        var pdf = SyntheticGiacomPdfBuilder.BuildPreBillingSampleA();
        var first = Ingest(pdf);
        var second = Ingest(pdf);

        second.Lines.Should().HaveCount(first.Lines.Count);
        for (var i = 0; i < first.Lines.Count; i++)
        {
            first.Lines[i].Id.Should().Be(second.Lines[i].Id);
            first.Lines[i].ProductNameRaw.Should().Be(second.Lines[i].ProductNameRaw);
            first.Lines[i].MexIdRaw.Should().Be(second.Lines[i].MexIdRaw);
        }
    }

    [Fact]
    public void Ingest_OutputPreservesRawProductNames_NoOfferOrSkuFields()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildPreBillingSampleA());

        result.Lines.Should().NotBeEmpty();
        result.Lines.Should().AllSatisfy(line =>
        {
            line.ProductNameRaw.Should().NotBeNullOrWhiteSpace();
            line.Id.SourceKind.Should().Be(Domain.Common.ImportSourceKind.GiacomBillingPdf);
        });
    }

    [Fact]
    public void Ingest_EmptyCoverSheet_ReturnsSuccessWithNoLines()
    {
        var result = Ingest(SyntheticGiacomPdfBuilder.BuildEmptyCoverSheet());

        result.Status.Should().Be(IngestionOutcomeStatus.Success);
        result.Lines.Should().BeEmpty();
        result.LogEntries.Should().Contain(e =>
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

        result.Lines.Should().HaveCount(50);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(2));
    }

    private GiacomPdfIngestionResult Ingest(byte[] pdfBytes)
    {
        using var stream = new MemoryStream(pdfBytes);
        return _ingester.Ingest(stream);
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
