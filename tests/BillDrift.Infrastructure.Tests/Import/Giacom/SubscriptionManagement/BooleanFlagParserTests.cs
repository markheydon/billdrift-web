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
        Assert.Equal(expected, BooleanFlagParser.Parse(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Blank_values_are_absent(string? raw)
    {
        Assert.Null(BooleanFlagParser.Parse(raw));
        Assert.True(BooleanFlagParser.IsRecognised(raw));
    }

    [Fact]
    public void Unrecognised_non_blank_value_parses_absent()
    {
        Assert.Null(BooleanFlagParser.Parse("maybe"));
    }
}
