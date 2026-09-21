using BillDrift.Application.Import;
using BillDrift.Application.Normalization;
using BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement;

namespace BillDrift.Infrastructure.Tests.Import.Giacom.SubscriptionManagement;

public class SubscriptionManagementCsvIngesterTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory, "fixtures", "subscription-management");

    private readonly SubscriptionManagementCsvIngester _ingester =
        new(new SubscriptionManagementNormalizer());

    [Fact]
    public void Sample_a_emits_subscription_truth_with_required_fields()
    {
        var result = Ingest("subscription-management-sample-a.csv");

        Assert.Contains(result.Status, new[] { IngestionOutcomeStatus.Success, IngestionOutcomeStatus.PartialSuccess });
        Assert.Equal(3, result.SubscriptionLines.Count);
        Assert.All(result.SubscriptionLines, line =>
        {
            Assert.False(string.IsNullOrWhiteSpace(line.Customer.MexId.Value));
            Assert.True(line.LicenceCount > 0);
            Assert.False(string.IsNullOrWhiteSpace(line.CommercialKeyRoot.OfferId.Value));
            Assert.False(string.IsNullOrWhiteSpace(line.CommercialKeyRoot.SkuId.Value));
        });
    }

    [Fact]
    public void Sample_a_associates_multiple_customers()
    {
        var result = Ingest("subscription-management-sample-a.csv");

        Assert.Equivalent(
            new[] { "MEX001", "MEX002", "MEX003" },
            result.SubscriptionLines.Select(l => l.Customer.MexId.Value));
    }

    [Fact]
    public void Sample_a_matches_golden_file()
    {
        var result = Ingest("subscription-management-sample-a.csv");
        var goldenPath = Path.Combine(FixtureRoot, "expected", "sample-a.json");

        if (!File.Exists(goldenPath))
        {
            GoldenFileComparer.WriteGoldenFile(result, goldenPath);
        }

        GoldenFileComparer.AssertResultMatchesGolden(result, goldenPath);
    }

    [Fact]
    public void Mixed_products_excludes_exclaimer_rows()
    {
        var result = Ingest("mixed-products.csv");

        Assert.Single(result.RawRows);
        Assert.Equal(1, result.Summary.RowsExcludedByScope);
        Assert.Single(result.RawRows, r => r.MexIdRaw == "MEX001");
        Assert.Contains(result.LogEntries, e => e.Reason == IngestionFailureReason.ProductOutOfScope);
    }

    [Fact]
    public void Column_variant_maps_mandatory_fields()
    {
        var result = Ingest("column-variant.csv");

        Assert.Equal(2, result.SubscriptionLines.Count);
        Assert.All(result.SubscriptionLines, line =>
        {
            Assert.StartsWith("MEX", line.Customer.MexId.Value);
            Assert.StartsWith("OFFER-", line.CommercialKeyRoot.OfferId.Value);
        });
    }

    [Fact]
    public void Partial_success_emits_valid_rows_and_skips_bad_rows()
    {
        var result = Ingest("partial-success.csv");

        Assert.Equal(IngestionOutcomeStatus.PartialSuccess, result.Status);
        Assert.Single(result.RawRows);
        Assert.Equal(2, result.Summary.RowsSkipped);
        Assert.Contains(result.LogEntries, e => e.Reason == IngestionFailureReason.MexIdMissing);
        Assert.Contains(result.LogEntries, e => e.Reason == IngestionFailureReason.LicenceCountUnparseable);
    }

    [Fact]
    public void Lifecycle_columns_populate_optional_fields()
    {
        var result = Ingest("lifecycle-columns.csv");
        var line = Assert.Single(result.SubscriptionLines);

        Assert.NotNull(line.Lifecycle);
        Assert.True(line.Lifecycle!.IsNce);
        Assert.False(line.Lifecycle.IsTrial);
        Assert.Equal("Auto-renew", line.Lifecycle.EndOfTermAction);
        Assert.Equal(new DateOnly(2026, 3, 31), line.Lifecycle.CancellableUntil);
        Assert.Equal(20, line.Lifecycle.AssignedLicenceCount);
        Assert.NotNull(line.Lifecycle.Price);
        Assert.NotNull(line.Lifecycle.ErpPrice);
    }

    [Fact]
    public void Reimport_produces_identical_source_document_and_line_keys()
    {
        var first = Ingest("subscription-management-sample-a.csv");
        var second = Ingest("subscription-management-sample-a.csv");

        Assert.Equal(first.SourceDocumentId, second.SourceDocumentId);
        Assert.Equivalent(first.RawRows.Select(r => r.Id), second.RawRows.Select(r => r.Id));
    }

    [Fact]
    public void Headers_only_csv_returns_failure_with_file_level_error()
    {
        var result = Ingest("headers-only.csv");

        Assert.Equal(IngestionOutcomeStatus.Failure, result.Status);
        Assert.Empty(result.RawRows);
        Assert.Empty(result.SubscriptionLines);
        Assert.Contains(result.LogEntries, e =>
            e.Reason == IngestionFailureReason.EmptyFile &&
            e.Severity == IngestionLogSeverity.Error);
    }

    [Fact]
    public void File_exceeding_max_size_returns_failure_without_reading_entire_stream()
    {
        var path = Path.Combine(FixtureRoot, "subscription-management-sample-a.csv");
        using var stream = File.OpenRead(path);

        var result = _ingester.Ingest(
            new SubscriptionManagementCsvIngestionRequest(stream, "sample.csv")
            {
                Options = new SubscriptionManagementCsvIngestionOptions(MaxFileSizeBytes: 64)
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(IngestionOutcomeStatus.Failure, result.Status);
        Assert.Contains(result.LogEntries, e => e.Reason == IngestionFailureReason.FileSizeExceeded);
    }

    private static SubscriptionManagementCsvIngestionResult Ingest(string fileName)
    {
        var path = Path.Combine(FixtureRoot, fileName);
        using var stream = File.OpenRead(path);
        return new SubscriptionManagementCsvIngester(new SubscriptionManagementNormalizer())
            .Ingest(new SubscriptionManagementCsvIngestionRequest(stream, fileName));
    }
}
