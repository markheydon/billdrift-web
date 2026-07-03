using BillDrift.Domain.Common;

namespace BillDrift.Domain.Import;

/// <summary>
/// Raw row from the Giacom wholesale price list catalogue, preserving source values before normalization into <see cref="Billing.IntendedPrice"/>.
/// </summary>
/// <param name="Id">Composite idempotency key for re-import deduplication.</param>
/// <param name="OfferIdRaw">Offer ID text from the price list.</param>
/// <param name="SkuIdRaw">SKU ID text from the price list.</param>
/// <param name="TermRaw">Contract term text to be mapped during normalization.</param>
/// <param name="FrequencyRaw">Billing frequency text to be mapped during normalization.</param>
/// <param name="WholesaleRaw">Wholesale price text to be parsed into <see cref="Money"/>.</param>
/// <param name="RrpRaw">RRP text to be parsed into <see cref="Money"/>.</param>
/// <param name="MarginRaw">Absolute margin text, if present in the price list.</param>
/// <param name="MarginPercentRaw">Margin percentage text, if present in the price list.</param>
/// <param name="StatusRaw">Catalogue status text to be mapped to <see cref="PriceListStatus"/>.</param>
/// <param name="PlatformRaw">NCE/Legacy platform text as exported, when present.</param>
/// <param name="CurrencyRaw">Currency code text when present in the export.</param>
/// <param name="SourceDocumentId">Identifier of the source price list file.</param>
/// <param name="RowNumber">1-based row number within the price list for traceability.</param>
public sealed record RawPriceListRow(
    RawImportId Id,
    string OfferIdRaw,
    string SkuIdRaw,
    string TermRaw,
    string FrequencyRaw,
    string WholesaleRaw,
    string RrpRaw,
    string? MarginRaw,
    string? MarginPercentRaw,
    string StatusRaw,
    string? PlatformRaw,
    string? CurrencyRaw,
    string SourceDocumentId,
    int RowNumber);
