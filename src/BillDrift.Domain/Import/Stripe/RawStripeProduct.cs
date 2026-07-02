using BillDrift.Domain.Common;

namespace BillDrift.Domain.Import.Stripe;

/// <summary>
/// Raw Stripe product record from export, representing an entry in the customer billing catalogue.
/// </summary>
public sealed record RawStripeProduct(
    RawImportId Id,
    string ProductId,
    string Name,
    int SourceRowNumber,
    IReadOnlyDictionary<string, string> Metadata);
