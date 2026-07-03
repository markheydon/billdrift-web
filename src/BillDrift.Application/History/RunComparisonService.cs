using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.History;

/// <summary>Compares two stored reconciliation run result snapshots.</summary>
public sealed class RunComparisonService(StableMismatchKeyFactory keyFactory)
{
    /// <summary>Produces a structured comparison report between two runs.</summary>
    public RunComparisonReport Compare(
        RunId earlierRunId,
        RunId laterRunId,
        RunResultsSnapshot earlierSnapshot,
        RunResultsSnapshot laterSnapshot,
        MappingVersionReference earlierMapping,
        MappingVersionReference laterMapping,
        IReadOnlyList<InputSnapshotMetadata> earlierInputs,
        IReadOnlyList<InputSnapshotMetadata> laterInputs,
        bool includeInputDeltas = true)
    {
        var earlierMap = BuildMismatchMap(earlierSnapshot.Mismatches, earlierRunId);
        var laterMap = BuildMismatchMap(laterSnapshot.Mismatches, laterRunId);

        var newExceptions = laterMap.Keys
            .Except(earlierMap.Keys)
            .Select(key => new ComparedMismatch(key, laterMap[key].Mismatch, laterRunId))
            .ToList();

        var resolvedExceptions = earlierMap.Keys
            .Except(laterMap.Keys)
            .Select(key => new ComparedMismatch(key, earlierMap[key].Mismatch, earlierRunId))
            .ToList();

        var persisting = earlierMap.Keys
            .Intersect(laterMap.Keys)
            .Select(key =>
            {
                var earlier = earlierMap[key].Mismatch;
                var later = laterMap[key].Mismatch;
                var valuesChanged = !string.Equals(
                    earlier.ExpectedValue?.Trim(),
                    later.ExpectedValue?.Trim(),
                    StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        earlier.ActualValue?.Trim(),
                        later.ActualValue?.Trim(),
                        StringComparison.OrdinalIgnoreCase);

                var mappingDriven = valuesChanged && IsMappingRelated(later.Type);
                return new PersistingMismatch(key, earlier, later, valuesChanged, MayBeMappingDriven: mappingDriven);
            })
            .ToList();

        var inputSummaries = includeInputDeltas
            ? BuildInputSummaries(earlierInputs, laterInputs)
            : [];

        return new RunComparisonReport(
            earlierRunId,
            laterRunId,
            DateTimeOffset.UtcNow,
            !string.Equals(earlierMapping.ContentHash, laterMapping.ContentHash, StringComparison.Ordinal),
            inputSummaries,
            new ExceptionDeltaReport(newExceptions, resolvedExceptions, persisting),
            new ProposalDeltaReport(earlierSnapshot.ProposedChanges.Count, laterSnapshot.ProposedChanges.Count));
    }

    private Dictionary<StableMismatchKey, ComparedMismatch> BuildMismatchMap(
        IReadOnlyList<Mismatch> mismatches,
        RunId runId)
    {
        var map = new Dictionary<StableMismatchKey, ComparedMismatch>();
        foreach (var mismatch in mismatches)
        {
            var key = keyFactory.Create(mismatch);
            map[key] = new ComparedMismatch(key, mismatch, runId);
        }

        return map;
    }

    private static List<InputChangeSummary> BuildInputSummaries(
        IReadOnlyList<InputSnapshotMetadata> earlierInputs,
        IReadOnlyList<InputSnapshotMetadata> laterInputs)
    {
        var summaries = new List<InputChangeSummary>();
        foreach (var domain in Enum.GetValues<InputDomainType>())
        {
            var earlier = earlierInputs.FirstOrDefault(i => i.Domain == domain);
            var later = laterInputs.FirstOrDefault(i => i.Domain == domain);
            summaries.Add(new InputChangeSummary(
                domain,
                earlier?.RecordCount ?? 0,
                later?.RecordCount ?? 0,
                earlier?.ContentFingerprint,
                later?.ContentFingerprint,
                !string.Equals(earlier?.ContentFingerprint, later?.ContentFingerprint, StringComparison.Ordinal)));
        }

        return summaries;
    }

    private static bool IsMappingRelated(MismatchType type) =>
        type is MismatchType.MappingMissing or MismatchType.MappingAmbiguous;
}
