using BillDrift.Domain.Common;
using BillDrift.Infrastructure.Import.Giacom.RetailPricing;

namespace BillDrift.Infrastructure.Tests.Import.Giacom.RetailPricing;

public sealed class PlatformClassifierTests
{
    [Theory]
    [InlineData("NCE", PricingPlatform.Nce)]
    [InlineData("New Commerce Experience", PricingPlatform.Nce)]
    [InlineData("Legacy", PricingPlatform.Legacy)]
    [InlineData("Old Commerce", PricingPlatform.Legacy)]
    [InlineData(null, PricingPlatform.Unknown)]
    [InlineData("", PricingPlatform.Unknown)]
    public void Classify_maps_known_platform_values(string? raw, PricingPlatform expected)
    {
        var platform = PlatformClassifier.Classify(raw, out var unrecognised);

        platform.Should().Be(expected);
        unrecognised.Should().BeFalse();
    }

    [Fact]
    public void Classify_flags_unrecognised_values()
    {
        var platform = PlatformClassifier.Classify("Azure Marketplace", out var unrecognised);

        platform.Should().Be(PricingPlatform.Unknown);
        unrecognised.Should().BeTrue();
    }
}
