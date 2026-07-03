using BillDrift.Application.History;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.History;

public sealed class PricingDriftAnalyzerTests
{
    private readonly PricingDriftAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_returns_empty_for_no_matching_runs()
    {
        var key = CommercialKey.Create(OfferId.Create("OFFER1"), SkuId.Create("SKU1"), Term.P1Y, BillingFrequency.Annual);
        var entries = _analyzer.Analyze(key, []);
        entries.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_accepts_run_snapshots_without_throwing()
    {
        var key = CommercialKey.Create(OfferId.Create("OFFER1"), SkuId.Create("SKU1"), Term.P1Y, BillingFrequency.Annual);
        var runs = new List<PricingDriftAnalyzer.PricingRunSnapshot>
        {
            new(RunId.New(), DateTimeOffset.UtcNow, [], [])
        };

        var act = () => _analyzer.Analyze(key, runs);
        act.Should().NotThrow();
    }
}
