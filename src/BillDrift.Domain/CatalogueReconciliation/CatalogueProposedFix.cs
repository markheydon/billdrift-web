using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>Approval-ready corrective action for a catalogue exception.</summary>
public sealed record CatalogueProposedFix(
    CatalogueProposedFixId Id,
    CatalogueExceptionId ExceptionId,
    CatalogueProposedActionType ActionType,
    IdempotencyKey IdempotencyKey,
    CommercialKeyRoot? CommercialKeyRoot,
    CommercialKey? CommercialKey,
    IReadOnlyDictionary<string, string> PriorState,
    IReadOnlyDictionary<string, string> ProposedState,
    string Rationale,
    bool IsActionable);
