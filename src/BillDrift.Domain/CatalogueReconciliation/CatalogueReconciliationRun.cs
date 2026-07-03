namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>Complete output of a catalogue reconciliation run.</summary>
public sealed record CatalogueReconciliationRun(
    CatalogueRunId RunId,
    DateTimeOffset ExecutedAt,
    CatalogueReconciliationInputs Inputs,
    IReadOnlyList<CatalogueException> Exceptions,
    IReadOnlyList<CatalogueProposedFix> ProposedFixes,
    CatalogueReconciliationSummary Summary,
    CatalogueReconciliationOptions Options);
