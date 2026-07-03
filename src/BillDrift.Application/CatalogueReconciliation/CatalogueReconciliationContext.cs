using BillDrift.Application.Reconciliation.Indexing;
using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Common;

namespace BillDrift.Application.CatalogueReconciliation;

/// <summary>Mutable state shared across catalogue reconciliation pipeline stages.</summary>
public sealed class CatalogueReconciliationContext
{
    /// <summary>Creates a context for a catalogue reconciliation run.</summary>
    public CatalogueReconciliationContext(
        CatalogueRunId runId,
        CatalogueReconciliationInputs inputs,
        CatalogueReconciliationOptions options)
    {
        RunId = runId;
        Inputs = inputs;
        Options = options;
    }

    /// <summary>Run identifier assigned for this execution.</summary>
    public CatalogueRunId RunId { get; }

    /// <summary>Input snapshot.</summary>
    public CatalogueReconciliationInputs Inputs { get; }

    /// <summary>Run options.</summary>
    public CatalogueReconciliationOptions Options { get; }

    /// <summary>Stripe catalogue index built from inputs.</summary>
    public StripeCatalogueSnapshotIndex? CatalogueIndex { get; set; }

    /// <summary>Product mapping index.</summary>
    public ProductMappingIndex? ProductMappingIndex { get; set; }

    /// <summary>Intended price index.</summary>
    public IntendedPriceIndex? IntendedPriceIndex { get; set; }

    /// <summary>Detected exceptions.</summary>
    public List<CatalogueException> Exceptions { get; } = [];

    /// <summary>Proposed fixes.</summary>
    public List<CatalogueProposedFix> ProposedFixes { get; } = [];

    /// <summary>Commercial roots with duplicate-product conflicts (suppress missing-product checks).</summary>
    public HashSet<CommercialKeyRoot> DuplicateProductRoots { get; } = [];

    /// <summary>Validation error message when inputs are invalid.</summary>
    public string? ValidationError { get; set; }

    /// <summary>Count of mapped products checked.</summary>
    public int MappedProductsChecked { get; set; }
}
