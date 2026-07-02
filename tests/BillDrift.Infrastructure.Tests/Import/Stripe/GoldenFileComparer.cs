using System.Text.Json;
using BillDrift.Application.Import;

namespace BillDrift.Infrastructure.Tests.Import.Stripe;

public static class GoldenFileComparer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void AssertBundleMatchesGolden(StripeCsvIngestionResult actual, string goldenFilePath)
    {
        var expectedJson = File.ReadAllText(goldenFilePath);
        var expected = JsonSerializer.Deserialize<GoldenBundle>(expectedJson, JsonOptions)
            ?? throw new InvalidOperationException($"Golden file empty: {goldenFilePath}");

        actual.SubscriptionItems.Should().HaveCount(expected.SubscriptionItems.Count);
        for (var i = 0; i < expected.SubscriptionItems.Count; i++)
        {
            var exp = expected.SubscriptionItems[i];
            var act = actual.SubscriptionItems[i];
            act.CustomerId.Should().Be(exp.CustomerId);
            act.SubscriptionId.Should().Be(exp.SubscriptionId);
            act.SubscriptionItemId.Should().Be(exp.SubscriptionItemId);
            act.ProductId.Should().Be(exp.ProductId);
            act.PriceId.Should().Be(exp.PriceId);
            act.Quantity.Should().Be(exp.Quantity);
            act.SubscriptionStatus.Should().Be(exp.SubscriptionStatus);
            act.Id.SourceLineKey.Should().Be(exp.SourceLineKey);
        }

        actual.Products.Should().HaveCount(expected.Products.Count);
        actual.Prices.Should().HaveCount(expected.Prices.Count);
    }

    public static void WriteGoldenFile(StripeCsvIngestionResult result, string goldenFilePath)
    {
        var golden = new GoldenBundle
        {
            SubscriptionItems = result.SubscriptionItems.Select(i => new GoldenSubscriptionItem
            {
                CustomerId = i.CustomerId,
                SubscriptionId = i.SubscriptionId,
                SubscriptionItemId = i.SubscriptionItemId,
                ProductId = i.ProductId,
                PriceId = i.PriceId,
                Quantity = i.Quantity,
                SubscriptionStatus = i.SubscriptionStatus,
                SourceLineKey = i.Id.SourceLineKey
            }).ToList(),
            Products = result.Products.Select(p => p.ProductId).ToList(),
            Prices = result.Prices.Select(p => p.PriceId).ToList()
        };

        Directory.CreateDirectory(Path.GetDirectoryName(goldenFilePath)!);
        File.WriteAllText(goldenFilePath, JsonSerializer.Serialize(golden, JsonOptions));
    }

    private sealed class GoldenBundle
    {
        public List<GoldenSubscriptionItem> SubscriptionItems { get; set; } = [];
        public List<string> Products { get; set; } = [];
        public List<string> Prices { get; set; } = [];
    }

    private sealed class GoldenSubscriptionItem
    {
        public string CustomerId { get; set; } = string.Empty;
        public string SubscriptionId { get; set; } = string.Empty;
        public string SubscriptionItemId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string PriceId { get; set; } = string.Empty;
        public long Quantity { get; set; }
        public string SubscriptionStatus { get; set; } = string.Empty;
        public string SourceLineKey { get; set; } = string.Empty;
    }
}
