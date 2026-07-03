using BillDrift.Domain.Common;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Domain.History;

/// <summary>Single pricing drift event in a commercial key timeline.</summary>
public sealed record PricingDriftTimelineEntry(
    CommercialKey CommercialKey,
    RunId RunId,
    DateTimeOffset RunDate,
    PricingDriftEventType EventType,
    decimal? IntendedAmount = null,
    decimal? OverrideAmount = null,
    decimal? StripeCatalogueAmount = null,
    string? Currency = null,
    int? LagRunsPersisted = null);
