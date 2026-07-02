using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;

namespace BillDrift.Application.Reconciliation.Matching;

/// <summary>
/// Result of resolving product identity for a source billing line.
/// </summary>
/// <param name="CommercialKeyRoot">Resolved offer/SKU when known.</param>
/// <param name="CommercialKey">Full commercial key including term and frequency when known.</param>
/// <param name="Confidence">Match confidence for the resolution path taken.</param>
/// <param name="ResolutionPath">Which priority step produced this resolution.</param>
/// <param name="Mapping">Product mapping used, if any.</param>
public sealed record CommercialKeyResolution(
    CommercialKeyRoot? CommercialKeyRoot,
    CommercialKey? CommercialKey,
    MatchConfidence Confidence,
    ProductResolutionPath ResolutionPath,
    ProductMapping? Mapping);
