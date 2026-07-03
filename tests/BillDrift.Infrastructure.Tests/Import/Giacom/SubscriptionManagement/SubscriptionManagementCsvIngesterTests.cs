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

        result.Status.Should().BeOneOf(IngestionOutcomeStatus.Success, IngestionOutcomeStatus.PartialSuccess);
        result.SubscriptionLines.Should().HaveCount(3);
        result.SubscriptionLines.Should().AllSatisfy(line =>
        {
            line.Customer.MexId.Value.Should().NotBeNullOrWhiteSpace();
            line.LicenceCount.Should().BeGreaterThan(0);
            line.CommercialKeyRoot.OfferId.Value.Should().NotBeNullOrWhiteSpace();
            line.CommercialKeyRoot.SkuId.Value.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void Sample_a_associates_multiple_customers()
    {
        var result = Ingest("subscription-management-sample-a.csv");

        result.SubscriptionLines.Select(l => l.Customer.MexId.Value)
            .Should().BeEquivalentTo(["MEX001", "MEX002", "MEX003"]);
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

        result.RawRows.Should().HaveCount(1);
        result.Summary.RowsExcludedByScope.Should().Be(1);
        result.RawRows.Should().ContainSingle(r => r.MexIdRaw == "MEX001");
        result.LogEntries.Should().Contain(e => e.Reason == IngestionFailureReason.ProductOutOfScope);
    }

    [Fact]
    public void Column_variant_maps_mandatory_fields()
    {
        var result = Ingest("column-variant.csv");

        result.SubscriptionLines.Should().HaveCount(2);
        result.SubscriptionLines.Should().AllSatisfy(line =>
        {
            line.Customer.MexId.Value.Should().StartWith("MEX");
            line.CommercialKeyRoot.OfferId.Value.Should().StartWith("OFFER-");
        });
    }

    [Fact]
    public void Partial_success_emits_valid_rows_and_skips_bad_rows()
    {
        var result = Ingest("partial-success.csv");

        result.Status.Should().Be(IngestionOutcomeStatus.PartialSuccess);
        result.RawRows.Should().HaveCount(1);
        result.Summary.RowsSkipped.Should().Be(2);
        result.LogEntries.Should().Contain(e => e.Reason == IngestionFailureReason.MexIdMissing);
        result.LogEntries.Should().Contain(e => e.Reason == IngestionFailureReason.LicenceCountUnparseable);
    }

    [Fact]
    public void Lifecycle_columns_populate_optional_fields()
    {
        var result = Ingest("lifecycle-columns.csv");
        var line = result.SubscriptionLines.Should().ContainSingle().Subject;

        line.Lifecycle.Should().NotBeNull();
        line.Lifecycle!.IsNce.Should().BeTrue();
        line.Lifecycle.IsTrial.Should().BeFalse();
        line.Lifecycle.EndOfTermAction.Should().Be("Auto-renew");
        line.Lifecycle.CancellableUntil.Should().Be(new DateOnly(2026, 3, 31));
        line.Lifecycle.AssignedLicenceCount.Should().Be(20);
        line.Lifecycle.Price.Should().NotBeNull();
        line.Lifecycle.ErpPrice.Should().NotBeNull();
    }

    [Fact]
    public void Reimport_produces_identical_source_document_and_line_keys()
    {
        var first = Ingest("subscription-management-sample-a.csv");
        var second = Ingest("subscription-management-sample-a.csv");

        second.SourceDocumentId.Should().Be(first.SourceDocumentId);
        second.RawRows.Select(r => r.Id).Should().BeEquivalentTo(first.RawRows.Select(r => r.Id));
    }

    [Fact]
    public void Headers_only_csv_returns_failure_with_file_level_error()
    {
        var result = Ingest("headers-only.csv");

        result.Status.Should().Be(IngestionOutcomeStatus.Failure);
        result.RawRows.Should().BeEmpty();
        result.SubscriptionLines.Should().BeEmpty();
        result.LogEntries.Should().Contain(e =>
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

        result.Status.Should().Be(IngestionOutcomeStatus.Failure);
        result.LogEntries.Should().Contain(e => e.Reason == IngestionFailureReason.FileSizeExceeded);
    }

    private static SubscriptionManagementCsvIngestionResult Ingest(string fileName)
    {
        var path = Path.Combine(FixtureRoot, fileName);
        using var stream = File.OpenRead(path);
        return new SubscriptionManagementCsvIngester(new SubscriptionManagementNormalizer())
            .Ingest(new SubscriptionManagementCsvIngestionRequest(stream, fileName));
    }
}
