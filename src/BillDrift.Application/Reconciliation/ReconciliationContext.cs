using BillDrift.Application.Classification;
using BillDrift.Application.Reconciliation.Indexing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.Reconciliation;

/// <summary>
/// Mutable per-run workspace for the reconciliation pipeline. Not exposed outside Application layer.
/// </summary>
public sealed class ReconciliationContext
{
    /// <summary>Original reconciliation request.</summary>
    public ReconciliationRequest Request { get; }

    /// <summary>Resolved run identifier.</summary>
    public RunId RunId { get; }

    /// <summary>Resolved options with defaults applied.</summary>
    public ReconciliationOptions Options { get; }

    /// <summary>Commercial key to winning intended price index.</summary>
    public IntendedPriceIndex IntendedPriceIndex { get; set; } = null!;

    /// <summary>Stripe product and price lookup index.</summary>
    public StripeCatalogueIndex StripeCatalogueIndex { get; set; } = null!;

    /// <summary>Product mapping lookup index.</summary>
    public ProductMappingIndex ProductMappingIndex { get; set; } = null!;

    /// <summary>Accumulated match groups (mutable during build).</summary>
    public List<EntityMatchGroup> MatchGroups { get; } = [];

    /// <summary>Detected mismatches (append-only).</summary>
    public List<Mismatch> Mismatches { get; } = [];

    /// <summary>Proposed corrective actions (append-only).</summary>
    public List<ProposedChange> ProposedChanges { get; } = [];

    /// <summary>Item classifications for this run, when supplied or enriched.</summary>
    public ClassificationContext? Classifications { get; set; }

    private int _mismatchSequence;

    /// <summary>
    /// Creates a new reconciliation context for the given request.
    /// </summary>
    /// <param name="request">The reconciliation request to process.</param>
    public ReconciliationContext(ReconciliationRequest request)
    {
        Request = request;
        RunId = request.RunId ?? RunId.New();
        Options = request.Options ?? new ReconciliationOptions();
        Classifications = request.Classifications;
    }

    /// <summary>
    /// Generates the next deterministic mismatch identifier for this run.
    /// </summary>
    /// <returns>A mismatch ID unique within this run.</returns>
    public MismatchId NextMismatchId()
    {
        var sequence = _mismatchSequence++;
        var bytes = new byte[16];
        RunId.Value.TryWriteBytes(bytes);
        bytes[15] = (byte)(sequence & 0xFF);
        bytes[14] = (byte)((sequence >> 8) & 0xFF);
        return MismatchId.FromGuid(new Guid(bytes));
    }

    /// <summary>
    /// Generates the next deterministic proposed change identifier for this run.
    /// </summary>
    /// <param name="mismatchId">The mismatch being addressed.</param>
    /// <returns>A proposed change ID linked to the mismatch.</returns>
    public ProposedChangeId NextProposedChangeId(MismatchId mismatchId) =>
        ProposedChangeId.FromGuid(mismatchId.Value);

    /// <summary>
    /// Creates a deterministic match group ID from customer and commercial key.
    /// </summary>
    /// <param name="customer">Customer identity for the group.</param>
    /// <param name="key">Commercial key when known.</param>
    /// <param name="index">Disambiguation index for duplicate keys.</param>
    /// <returns>A deterministic match group ID.</returns>
    public static MatchGroupId CreateMatchGroupId(CustomerIdentity customer, CommercialKey? key, int index)
    {
        var composite = key.HasValue
            ? $"{customer.MexId.Value}:{key.Value.OfferId.Value}:{key.Value.SkuId.Value}:{(int)key.Value.Term}:{(int)key.Value.Frequency}:{index}"
            : $"{customer.MexId.Value}:null:{index}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(composite));
        return MatchGroupId.FromGuid(new Guid(hash.AsSpan(0, 16)));
    }
}
