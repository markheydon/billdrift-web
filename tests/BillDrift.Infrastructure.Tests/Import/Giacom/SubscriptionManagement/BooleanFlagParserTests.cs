using BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement;

namespace BillDrift.Infrastructure.Tests.Import.Giacom.SubscriptionManagement;

public class BooleanFlagParserTests
{
    [Theory]
    [InlineData("Y", true)]
    [InlineData("yes", true)]
    [InlineData("True", true)]
    [InlineData("1", true)]
    [InlineData("N", false)]
    [InlineData("no", false)]
    [InlineData("0", false)]
    public void Recognised_values_parse(string raw, bool expected)
    {
        BooleanFlagParser.Parse(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Blank_values_are_absent(string? raw)
    {
        BooleanFlagParser.Parse(raw).Should().BeNull();
        BooleanFlagParser.IsRecognised(raw).Should().BeTrue();
    }

    [Fact]
    public void Unrecognised_non_blank_value_parses_absent()
    {
        BooleanFlagParser.Parse("maybe").Should().BeNull();
    }
}
