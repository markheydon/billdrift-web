using BillDrift.Application.Reconciliation.ExceptionSurfacing.Detection;
using BillDrift.Application.Reconciliation.ExceptionSurfacing.Evidence;
using BillDrift.Application.Reconciliation.ExceptionSurfacing.Mapping;

namespace BillDrift.Application.Reconciliation.ExceptionSurfacing.Phases;

/// <summary>Phase 1 — collects mismatch-mapped and derived exception candidates.</summary>
public sealed class CollectPhase
{
    private readonly MismatchToExceptionMapper _mapper = new();
    private readonly EvidenceBuilder _evidenceBuilder = new();
    private readonly OrphanedStripeDetector _orphanedDetector = new();
    private readonly MexIdMismatchDetector _mexIdDetector = new();
    private readonly ProductMismatchDetector _productDetector = new();

    /// <summary>Builds raw surfaced exception candidates into the context.</summary>
    public void Execute(SurfacingContext context)
    {
        context.Candidates.Clear();

        foreach (var mismatch in context.Run.Mismatches)
        {
            var mapped = _mapper.Map(mismatch, context);
            var group = context.FindMatchGroup(mismatch);
            var evidence = _evidenceBuilder.Build(mismatch, group, context);
            context.Candidates.Add(mapped with { Evidence = evidence });
        }

        foreach (var derived in _orphanedDetector.Detect(context)
                     .Concat(_mexIdDetector.Detect(context))
                     .Concat(_productDetector.Detect(context)))
        {
            var evidence = _evidenceBuilder.BuildForDerived(derived, context);
            context.Candidates.Add(derived with { Evidence = evidence });
        }
    }
}
