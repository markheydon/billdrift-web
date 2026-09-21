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

        Assert.Equal(expected.SubscriptionItems.Count, actual.SubscriptionItems.Count);
        for (var i = 0; i < expected.SubscriptionItems.Count; i++)
        {
            var exp = expected.SubscriptionItems[i];
            var act = actual.SubscriptionItems[i];
            Assert.Equal(exp.CustomerId, act.CustomerId);
            Assert.Equal(exp.SubscriptionId, act.SubscriptionId);
            Assert.Equal(exp.SubscriptionItemId, act.SubscriptionItemId);
            Assert.Equal(exp.ProductId, act.ProductId);
            Assert.Equal(exp.PriceId, act.PriceId);
            Assert.Equal(exp.Quantity, act.Quantity);
            Assert.Equal(exp.SubscriptionStatus, act.SubscriptionStatus);
            Assert.Equal(exp.SourceLineKey, act.Id.SourceLineKey);
        }

        Assert.Equal(expected.Products.Count, actual.Products.Count);
        Assert.Equal(expected.Prices.Count, actual.Prices.Count);
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
