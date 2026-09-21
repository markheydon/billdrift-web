using BillDrift.Domain.Common;
using BillDrift.Infrastructure.Import.Giacom.RetailPricing;

namespace BillDrift.Infrastructure.Tests.Import.Giacom.RetailPricing;

public sealed class TermFrequencyParserTests
{
    [Theory]
    [InlineData("Annual", Term.Annual)]
    [InlineData("P1Y", Term.Annual)]
    [InlineData("P1M", Term.Monthly)]
    [InlineData("Triennial", Term.Triennial)]
    [InlineData("P3Y", Term.Triennial)]
    public void TryParseTerm_maps_known_values(string raw, Term expected)
    {
        Assert.True(TermFrequencyParser.TryParseTerm(raw, out var term));
        Assert.Equal(expected, term);
    }

    [Theory]
    [InlineData("Monthly", BillingFrequency.Monthly)]
    [InlineData("Annual", BillingFrequency.Annual)]
    [InlineData("yearly", BillingFrequency.Annual)]
    public void TryParseFrequency_maps_known_values(string raw, BillingFrequency expected)
    {
        Assert.True(TermFrequencyParser.TryParseFrequency(raw, out var frequency));
        Assert.Equal(expected, frequency);
    }

    [Fact]
    public void TryParseTerm_rejects_unknown_values()
    {
        Assert.False(TermFrequencyParser.TryParseTerm("Biennial", out var term));
        Assert.Equal(Term.Unknown, term);
    }
}
