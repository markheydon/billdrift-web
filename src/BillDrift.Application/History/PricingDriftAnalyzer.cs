using System.Text.Json;
using System.Text.Json.Serialization;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.History;

/// <summary>Analyzes pricing drift across stored run input snapshots.</summary>
public sealed class PricingDriftAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Builds a pricing drift timeline for a commercial key across runs.</summary>
    public IReadOnlyList<PricingDriftTimelineEntry> Analyze(
        CommercialKey commercialKey,
        IReadOnlyList<PricingRunSnapshot> runs)
    {
        var entries = new List<PricingDriftTimelineEntry>();
        PricingState? previous = null;
        var lagStartRunIndex = -1;

        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            var current = ExtractState(commercialKey, run);
            if (current is null)
            {
                previous = null;
                continue;
            }

            if (previous is null)
            {
                previous = current;
                continue;
            }

            if (current.CatalogueRrp != previous.CatalogueRrp && current.OverrideAmount is null && previous.OverrideAmount is null)
            {
                entries.Add(CreateEntry(commercialKey, run, PricingDriftEventType.RrpChanged, current));
            }

            if (current.OverrideAmount is not null && previous.OverrideAmount is null)
            {
                entries.Add(CreateEntry(commercialKey, run, PricingDriftEventType.OverrideAdded, current));
            }
            else if (current.OverrideAmount is null && previous.OverrideAmount is not null)
            {
                entries.Add(CreateEntry(commercialKey, run, PricingDriftEventType.OverrideRemoved, current));
            }
            else if (current.OverrideAmount != previous.OverrideAmount && current.OverrideAmount is not null)
            {
                entries.Add(CreateEntry(commercialKey, run, PricingDriftEventType.OverrideAdded, current));
            }

            if (current.StripeAmount != previous.StripeAmount && current.StripeAmount is not null)
            {
                var aligned = current.StripeAmount == current.EffectiveAmount;
                entries.Add(CreateEntry(
                    commercialKey,
                    run,
                    aligned ? PricingDriftEventType.CatalogueAligned : PricingDriftEventType.StripePriceChanged,
                    current,
                    lagRunsPersisted: aligned ? 0 : null));
                if (aligned)
                {
                    lagStartRunIndex = -1;
                }
            }

            if (current.StripeAmount is null && current.EffectiveAmount is not null)
            {
                lagStartRunIndex = lagStartRunIndex < 0 ? i : lagStartRunIndex;
                var lag = i - lagStartRunIndex + 1;
                entries.Add(CreateEntry(commercialKey, run, PricingDriftEventType.CatalogueMissing, current, lagRunsPersisted: lag));
            }
            else if (current.StripeAmount is not null && previous.StripeAmount is null)
            {
                entries.Add(CreateEntry(commercialKey, run, PricingDriftEventType.CatalogueAligned, current, lagRunsPersisted: 0));
                lagStartRunIndex = -1;
            }

            previous = current;
        }

        return entries;
    }

    /// <summary>Deserializes intended pricing records from an input blob.</summary>
    public static IReadOnlyList<IntendedPrice> DeserializeIntendedPrices(string json)
    {
        var wrapper = JsonSerializer.Deserialize<InputBlobWrapper<IntendedPrice>>(json, JsonOptions);
        return wrapper?.Records ?? [];
    }

    /// <summary>Deserializes Stripe billing records from an input blob.</summary>
    public static IReadOnlyList<StripeBillingItem> DeserializeStripeItems(string json)
    {
        var wrapper = JsonSerializer.Deserialize<InputBlobWrapper<StripeBillingItem>>(json, JsonOptions);
        return wrapper?.Records ?? [];
    }

    private static PricingState? ExtractState(CommercialKey key, PricingRunSnapshot run)
    {
        var intended = run.IntendedPrices.FirstOrDefault(p => KeysMatch(p.Key, key));
        var stripe = run.StripeItems
            .Where(s => s.MappingMetadata.OfferId == key.OfferId && s.MappingMetadata.SkuId == key.SkuId &&
                        s.Frequency == key.Frequency)
            .Select(s => (decimal?)s.UnitAmount.Amount)
            .FirstOrDefault();

        if (intended is null && stripe is null)
        {
            return null;
        }

        var catalogueRrp = intended?.Source == PriceSource.Catalogue ? (decimal?)intended.Rrp.Amount : null;
        var overrideAmount = intended?.Source == PriceSource.ManualOverride ? (decimal?)intended.Rrp.Amount : null;
        var effective = overrideAmount ?? catalogueRrp ?? intended?.Rrp.Amount;
        var currency = intended?.Rrp.Currency.Value ?? run.StripeItems.FirstOrDefault()?.UnitAmount.Currency.Value;

        return new PricingState(catalogueRrp, overrideAmount, effective, stripe, currency);
    }

    private static bool KeysMatch(CommercialKey left, CommercialKey right) =>
        left.OfferId == right.OfferId &&
        left.SkuId == right.SkuId &&
        left.Term == right.Term &&
        left.Frequency == right.Frequency;

    private static PricingDriftTimelineEntry CreateEntry(
        CommercialKey commercialKey,
        PricingRunSnapshot run,
        PricingDriftEventType eventType,
        PricingState state,
        int? lagRunsPersisted = null) =>
        new(
            commercialKey,
            run.RunId,
            run.CompletedAt,
            eventType,
            state.CatalogueRrp,
            state.OverrideAmount,
            state.StripeAmount,
            state.Currency,
            lagRunsPersisted);

    private sealed record PricingState(
        decimal? CatalogueRrp,
        decimal? OverrideAmount,
        decimal? EffectiveAmount,
        decimal? StripeAmount,
        string? Currency);

    private sealed record InputBlobWrapper<T>(IReadOnlyList<T>? Records);

    /// <summary>Input snapshot for one run in pricing drift analysis.</summary>
    public sealed record PricingRunSnapshot(
        RunId RunId,
        DateTimeOffset CompletedAt,
        IReadOnlyList<IntendedPrice> IntendedPrices,
        IReadOnlyList<StripeBillingItem> StripeItems);
}
