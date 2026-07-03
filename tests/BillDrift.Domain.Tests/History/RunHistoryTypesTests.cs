using BillDrift.Domain.History;
using FluentAssertions;

namespace BillDrift.Domain.Tests.History;

public sealed class RunHistoryTypesTests
{
    [Fact]
    public void StableMismatchKey_truncates_long_values()
    {
        var longValue = new string('a', 600);
        var key = StableMismatchKey.Create(longValue);

        key.Value.Should().HaveLength(512);
    }

    [Fact]
    public void ReconciliationRunRecord_requires_all_input_domains()
    {
        var snapshots = Enum.GetValues<InputDomainType>()
            .Select(d => new InputSnapshotMetadata(d, true))
            .ToList();

        snapshots.Should().HaveCount(5);
    }

    [Fact]
    public void RunSummaryMetrics_clean_run_when_zero_mismatches()
    {
        var metrics = new RunSummaryMetrics(10, 0, new Dictionary<string, int>(), 0, true);
        metrics.CleanRun.Should().BeTrue();
    }
}
