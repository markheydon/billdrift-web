using System.Text;
using BillDrift.Application.Import;
using BillDrift.Infrastructure.Import.Stripe;

namespace BillDrift.Infrastructure.Tests.Import.Stripe;

public class StripeBillingCsvIngesterPerformanceTests
{
    [Fact]
    public void Thousand_row_bundle_completes_within_sixty_seconds()
    {
        var ingester = new StripeBillingCsvIngester();
        var subscriptionsCsv = BuildSyntheticSubscriptionsCsv(1000);
        var productsCsv = "id,Name\nprod_001,Product A\n";
        var pricesCsv = "id,Product ID,Currency,Amount\nprice_001,prod_001,gbp,10.00\n";

        var request = new StripeCsvIngestionRequest(
        [
            new StripeCsvFileInput(StripeCsvFileKind.Subscriptions, new MemoryStream(Encoding.UTF8.GetBytes(subscriptionsCsv))),
            new StripeCsvFileInput(StripeCsvFileKind.Products, new MemoryStream(Encoding.UTF8.GetBytes(productsCsv))),
            new StripeCsvFileInput(StripeCsvFileKind.Prices, new MemoryStream(Encoding.UTF8.GetBytes(pricesCsv)))
        ]);

        var started = DateTimeOffset.UtcNow;
        var result = ingester.Ingest(request, TestContext.Current.CancellationToken);
        var elapsed = DateTimeOffset.UtcNow - started;

        result.SubscriptionItems.Should().HaveCount(1000);
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(60));
    }

    private static string BuildSyntheticSubscriptionsCsv(int rowCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Customer ID,Customer Name,id,Subscription Item ID,Product ID,Price ID,Product Name,Quantity,Status,Amount,Interval,metadata[mex_id],metadata[offer_id],metadata[sku_id]");

        for (var i = 0; i < rowCount; i++)
        {
            sb.Append("cus_")
                .Append(i)
                .Append(",Customer ")
                .Append(i)
                .Append(",sub_")
                .Append(i)
                .Append(",si_")
                .Append(i)
                .Append(",prod_001,price_001,Product,1,active,10.00,month,MEX")
                .Append(i)
                .Append(",OFF1,SKU1")
                .AppendLine();
        }

        return sb.ToString();
    }
}
