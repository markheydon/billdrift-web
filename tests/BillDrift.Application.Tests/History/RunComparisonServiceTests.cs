using BillDrift.Application.History;
using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;
using FluentAssertions;

namespace BillDrift.Application.Tests.History;

public sealed class RunComparisonServiceTests
{
    private readonly RunComparisonService _service = new(new StableMismatchKeyFactory());

    [Fact]
    public void Compare_classifies_new_resolved_and_persisting_exceptions()
    {
        var earlierId = RunId.New();
        var laterId = RunId.New();
        var persistingMismatch = CreateMismatch(MismatchType.QuantityMismatch, "5", "3");
        var resolvedMismatch = CreateMismatch(MismatchType.PriceMismatch, "10", "12");
        var newMismatch = CreateMismatch(MismatchType.MissingInStripe);

        var earlier = CreateSnapshot(earlierId, [persistingMismatch, resolvedMismatch]);
        var later = CreateSnapshot(laterId, [persistingMismatch, newMismatch]);
        var mapping = new MappingVersionReference("v1", "hash", new DateOnly(2026, 1, 1));

        var report = _service.Compare(
            earlierId,
            laterId,
            earlier,
            later,
            mapping,
            mapping,
            [],
            []);

        report.ExceptionDeltas.NewExceptions.Should().HaveCount(1);
        report.ExceptionDeltas.ResolvedExceptions.Should().HaveCount(1);
        report.ExceptionDeltas.PersistingExceptions.Should().HaveCount(1);
    }

    [Fact]
    public void Compare_flags_mapping_version_change()
    {
        var earlierId = RunId.New();
        var laterId = RunId.New();
        var earlier = CreateSnapshot(earlierId, []);
        var later = CreateSnapshot(laterId, []);
        var earlierMapping = new MappingVersionReference("v1", "hash-a", new DateOnly(2026, 1, 1));
        var laterMapping = new MappingVersionReference("v2", "hash-b", new DateOnly(2026, 2, 1));

        var report = _service.Compare(earlierId, laterId, earlier, later, earlierMapping, laterMapping, [], []);

        report.MappingVersionChanged.Should().BeTrue();
    }

    private static RunResultsSnapshot CreateSnapshot(RunId runId, IReadOnlyList<Mismatch> mismatches) =>
        new(runId, [], mismatches, [], "hash");

    private static Mismatch CreateMismatch(MismatchType type, string? expected = null, string? actual = null) =>
        new(
            MismatchId.New(),
            type,
            MismatchSeverity.Warning,
            CustomerIdentity.Create(MexId.Create("MEX001")),
            null,
            new MismatchEntityRefs(),
            expected,
            actual,
            $"{type} description");
}
