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
        TermFrequencyParser.TryParseTerm(raw, out var term).Should().BeTrue();
        term.Should().Be(expected);
    }

    [Theory]
    [InlineData("Monthly", BillingFrequency.Monthly)]
    [InlineData("Annual", BillingFrequency.Annual)]
    [InlineData("yearly", BillingFrequency.Annual)]
    public void TryParseFrequency_maps_known_values(string raw, BillingFrequency expected)
    {
        TermFrequencyParser.TryParseFrequency(raw, out var frequency).Should().BeTrue();
        frequency.Should().Be(expected);
    }

    [Fact]
    public void TryParseTerm_rejects_unknown_values()
    {
        TermFrequencyParser.TryParseTerm("Biennial", out var term).Should().BeFalse();
        term.Should().Be(Term.Unknown);
    }
}
