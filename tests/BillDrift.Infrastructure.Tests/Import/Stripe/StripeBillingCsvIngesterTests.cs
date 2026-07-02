using BillDrift.Application.Import;
using BillDrift.Infrastructure.Import.Stripe;

namespace BillDrift.Infrastructure.Tests.Import.Stripe;

public class StripeBillingCsvIngesterTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory, "fixtures", "stripe-csv");

    private readonly StripeBillingCsvIngester _ingester = new();

    [Fact]
    public void Subscriptions_only_ingest_emits_items_with_required_ids()
    {
        var result = IngestSubscriptions("subscriptions-sample-a.csv");

        result.Status.Should().BeOneOf(IngestionOutcomeStatus.Success, IngestionOutcomeStatus.PartialSuccess);
        result.SubscriptionItems.Should().NotBeEmpty();
        result.SubscriptionItems.Should().AllSatisfy(i =>
        {
            i.CustomerId.Should().NotBeNullOrWhiteSpace();
            i.SubscriptionId.Should().NotBeNullOrWhiteSpace();
            i.ProductId.Should().NotBeNullOrWhiteSpace();
            i.PriceId.Should().NotBeNullOrWhiteSpace();
        });
        result.Products.Should().BeEmpty();
        result.Prices.Should().BeEmpty();
    }

    [Fact]
    public void Multi_item_subscription_emits_multiple_rows()
    {
        var result = IngestSubscriptions("subscriptions-sample-a.csv");

        var sub001Items = result.SubscriptionItems.Where(i => i.SubscriptionId == "sub_001").ToList();
        sub001Items.Should().HaveCount(2);
        sub001Items.Select(i => i.SubscriptionItemId).Should().BeEquivalentTo(["si_001", "si_002"]);
    }

    [Fact]
    public void Full_bundle_resolves_catalogue_ids()
    {
        var result = IngestBundle("subscriptions-sample-a.csv", "products-sample-a.csv", "prices-sample-a.csv");

        result.Products.Should().HaveCount(2);
        result.Prices.Should().HaveCount(2);

        foreach (var item in result.SubscriptionItems)
        {
            result.Products.Select(p => p.ProductId).Should().Contain(item.ProductId);
            result.Prices.Select(p => p.PriceId).Should().Contain(item.PriceId);
        }
    }

    [Fact]
    public void Full_bundle_matches_golden_file()
    {
        var result = IngestBundle("subscriptions-sample-a.csv", "products-sample-a.csv", "prices-sample-a.csv");
        var goldenPath = Path.Combine(FixtureRoot, "expected", "bundle-sample-a.json");

        if (!File.Exists(goldenPath))
        {
            GoldenFileComparer.WriteGoldenFile(result, goldenPath);
        }

        GoldenFileComparer.AssertBundleMatchesGolden(result, goldenPath);
    }

    [Fact]
    public void Subscriptions_only_matches_golden_file()
    {
        var result = IngestSubscriptions("subscriptions-sample-a.csv");
        var goldenPath = Path.Combine(FixtureRoot, "expected", "subscriptions-sample-a.json");

        if (!File.Exists(goldenPath))
        {
            GoldenFileComparer.WriteGoldenFile(result, goldenPath);
        }

        GoldenFileComparer.AssertBundleMatchesGolden(result, goldenPath);
    }

    [Fact]
    public void Mixed_status_default_filter_excludes_canceled()
    {
        var result = IngestSubscriptions("subscriptions-mixed-status.csv");

        result.SubscriptionItems.Should().NotContain(i => i.SubscriptionStatus.Equals("canceled", StringComparison.OrdinalIgnoreCase));
        result.Summary.SubscriptionsFilteredByStatus.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Mixed_status_inclusive_includes_canceled()
    {
        var result = Ingest(
            [FixtureFile(StripeCsvFileKind.Subscriptions, "subscriptions-mixed-status.csv")],
            new StripeCsvIngestionOptions(IncludeInactiveSubscriptions: true));

        result.SubscriptionItems.Should().Contain(i => i.SubscriptionStatus.Equals("canceled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Partial_metadata_emits_warnings_without_inventing_keys()
    {
        var result = IngestSubscriptions("subscriptions-partial-metadata.csv");

        result.SubscriptionItems.Should().HaveCount(3);
        result.Summary.MetadataWarnings.Should().BeGreaterThan(0);
        result.LogEntries.Should().Contain(e =>
            e.Reason == IngestionFailureReason.MetadataIncomplete ||
            e.Reason == IngestionFailureReason.MetadataInconsistent);
    }

    [Fact]
    public void Column_variant_fixture_maps_mandatory_fields()
    {
        var result = IngestSubscriptions("subscriptions-column-variant.csv");

        result.SubscriptionItems.Should().HaveCount(2);
        result.SubscriptionItems.Should().AllSatisfy(i =>
        {
            i.CustomerId.Should().Be("cus_A1");
            i.SubscriptionId.Should().Be("sub_001");
        });
    }

    [Fact]
    public void Partial_success_emits_valid_rows_and_skips_bad_row()
    {
        var result = IngestSubscriptions("subscriptions-partial-success.csv");

        result.Status.Should().Be(IngestionOutcomeStatus.PartialSuccess);
        result.SubscriptionItems.Should().HaveCount(2);
        result.Summary.SubscriptionItemsSkipped.Should().Be(1);
        result.LogEntries.Should().Contain(e => e.Reason == IngestionFailureReason.QuantityUnparseable);
    }

    [Fact]
    public void Reimport_produces_identical_bundle_and_line_keys()
    {
        var first = IngestBundle("subscriptions-sample-a.csv", "products-sample-a.csv", "prices-sample-a.csv");
        var second = IngestBundle("subscriptions-sample-a.csv", "products-sample-a.csv", "prices-sample-a.csv");

        second.BundleId.Should().Be(first.BundleId);
        second.SubscriptionItems.Select(i => i.Id).Should().BeEquivalentTo(first.SubscriptionItems.Select(i => i.Id));
    }

    [Fact]
    public void Missing_item_id_rows_get_distinct_line_keys_per_subscription_row()
    {
        const string csv = """
            Customer ID,Customer Name,id,Product ID,Price ID,Product Name,Quantity,Status
            cus_A1,Acme Corp,sub_001,prod_001,price_001,Office 365,1,active
            cus_A1,Acme Corp,sub_001,prod_002,price_002,Azure,2,active
            """;

        var result = Ingest(
        [
            new StripeCsvFileInput(
                StripeCsvFileKind.Subscriptions,
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv)),
                "no-item-id.csv")
        ]);

        result.SubscriptionItems.Should().HaveCount(2);
        result.SubscriptionItems.Select(i => i.Id.SourceLineKey).Should().OnlyHaveUniqueItems();
        result.SubscriptionItems.Should().AllSatisfy(i =>
        {
            i.SubscriptionItemId.Should().Be("sub_001");
            i.Id.SourceLineKey.Should().StartWith("sub_001:");
        });
    }

    private StripeCsvIngestionResult IngestSubscriptions(string fileName) =>
        Ingest([FixtureFile(StripeCsvFileKind.Subscriptions, fileName)]);

    private StripeCsvIngestionResult IngestBundle(string subs, string products, string prices) =>
        Ingest(
        [
            FixtureFile(StripeCsvFileKind.Subscriptions, subs),
            FixtureFile(StripeCsvFileKind.Products, products),
            FixtureFile(StripeCsvFileKind.Prices, prices)
        ]);

    private StripeCsvIngestionResult Ingest(
        IReadOnlyList<StripeCsvFileInput> files,
        StripeCsvIngestionOptions? options = null) =>
        _ingester.Ingest(new StripeCsvIngestionRequest(files, options), TestContext.Current.CancellationToken);

    private static StripeCsvFileInput FixtureFile(StripeCsvFileKind kind, string fileName)
    {
        var path = Path.Combine(FixtureRoot, fileName);
        return new StripeCsvFileInput(kind, System.IO.File.OpenRead(path), fileName);
    }
}
