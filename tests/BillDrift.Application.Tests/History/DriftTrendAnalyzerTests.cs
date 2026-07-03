using BillDrift.Application.History;
using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.History;

public sealed class DriftTrendAnalyzerTests
{
    private readonly DriftTrendAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_surfaces_recurring_drift_with_minimum_occurrences()
    {
        var key = StableMismatchKey.Create("mex001|offer/sku|quantitymismatch|qty");
        var entries = Enumerable.Range(0, 4)
            .Select(i => new DriftIndexEntry(
                key,
                RunId.New(),
                MexId.Create("MEX001"),
                null,
                MismatchType.QuantityMismatch,
                MismatchSeverity.Warning,
                MismatchId.New(),
                DateTimeOffset.UtcNow.AddMonths(-i),
                "Quantity mismatch"))
            .ToList();

        var trends = _analyzer.Analyze(entries, minOccurrences: 3);

        trends.Should().HaveCount(1);
        trends[0].OccurrenceCount.Should().BeGreaterThanOrEqualTo(3);
        trends[0].IsRecurring.Should().BeTrue();
    }

    [Fact]
    public void Analyze_excludes_transient_single_occurrence()
    {
        var key = StableMismatchKey.Create("mex001|offer/sku|quantitymismatch|qty");
        var entries = new[]
        {
            new DriftIndexEntry(
                key,
                RunId.New(),
                MexId.Create("MEX001"),
                null,
                MismatchType.QuantityMismatch,
                MismatchSeverity.Warning,
                MismatchId.New(),
                DateTimeOffset.UtcNow,
                "Quantity mismatch")
        };

        var trends = _analyzer.Analyze(entries, minOccurrences: 2);

        trends.Should().BeEmpty();
    }
}
