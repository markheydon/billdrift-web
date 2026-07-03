using BillDrift.Application.Import;
using BillDrift.Application.Normalization;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;

namespace BillDrift.Infrastructure.Import.Giacom.RetailPricing;

/// <summary>
/// Orchestrates the Giacom reseller price list CSV ingestion pipeline through resolved intended prices.
/// </summary>
public sealed class ResellerPricingCsvIngester : IResellerPricingCsvIngester
{
    private readonly ResellerPricingCsvRowReader _rowReader = new();
    private readonly RawPriceListRowMapper _mapper = new();
    private readonly IPriceListNormalizer _normalizer;
    private readonly IIntendedPriceResolver _resolver;

    public ResellerPricingCsvIngester(IPriceListNormalizer normalizer, IIntendedPriceResolver resolver)
    {
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <inheritdoc />
    public RetailPricingCsvIngestionResult Ingest(
        RetailPricingCsvIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.CatalogueContent);

        var options = request.Options ?? new RetailPricingCsvIngestionOptions();
        var ingestedAt = DateTimeOffset.UtcNow;
        var logs = new List<IngestionLogEntry>();

        byte[] bytes;
        using (request.CatalogueContent)
        {
            try
            {
                bytes = RetailPricingCsvContentReader.ReadBounded(
                    request.CatalogueContent,
                    options.MaxFileSizeBytes);
            }
            catch (RetailPricingUploadTooLargeException)
            {
                return FailureResult(string.Empty, ingestedAt, logs,
                    IngestionFailureReason.FileSizeExceeded,
                    $"CSV file exceeds maximum allowed size of {options.MaxFileSizeBytes} bytes.");
            }
        }

        if (bytes.Length == 0)
        {
            return FailureResult(string.Empty, ingestedAt, logs,
                IngestionFailureReason.EmptyFile, "CSV file is empty.");
        }

        var sourceDocumentId = RetailPricingFileIdentity.ComputeSourceDocumentId(bytes);
        cancellationToken.ThrowIfCancellationRequested();

        var readResult = _rowReader.Read(bytes);
        if (!readResult.IsSuccess)
        {
            logs.Add(CreateFileLog(
                IngestionLogSeverity.Error,
                readResult.FileFailure!.Value,
                readResult.FailureMessage ?? "CSV file could not be read.",
                sourceDocumentId));

            return FailureResult(sourceDocumentId, ingestedAt, logs,
                readResult.FileFailure.Value,
                readResult.FailureMessage ?? "CSV file could not be read.");
        }

        var rawCatalogueRows = new List<RawPriceListRow>();
        var cataloguePrices = new List<IntendedPrice>();
        var catalogueRowsRead = readResult.Rows.Count;
        var catalogueRowsSkipped = 0;
        var normalizationSkipped = 0;
        var duplicateKeyWarnings = 0;
        var catalogueByKey = new Dictionary<CommercialKey, IntendedPrice>();

        for (var index = 0; index < readResult.Rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowNumber = index + 1;
            var parsed = ResellerPricingCsvHeaderMap.ToParsedRow(
                readResult.Rows[index],
                readResult.FieldMap,
                rowNumber);

            var mapped = _mapper.Map(parsed, sourceDocumentId);
            if (mapped.SkipLog is not null)
            {
                catalogueRowsSkipped++;
                logs.Add(mapped.SkipLog);
                continue;
            }

            if (mapped.WarningLog is not null)
            {
                logs.Add(mapped.WarningLog);
            }

            if (mapped.Row is null)
            {
                catalogueRowsSkipped++;
                continue;
            }

            rawCatalogueRows.Add(mapped.Row);

            if (!options.NormalizeOutput)
            {
                continue;
            }

            try
            {
                var normalized = _normalizer.Normalize(mapped.Row);
                if (catalogueByKey.ContainsKey(normalized.Key))
                {
                    duplicateKeyWarnings++;
                    logs.Add(CreateRowLog(
                        IngestionLogSeverity.Warning,
                        IngestionFailureReason.DuplicateCommercialKey,
                        "Duplicate commercial key in catalogue; last row wins.",
                        rowNumber,
                        sourceDocumentId,
                        normalized.Key.ToString()));
                }

                catalogueByKey[normalized.Key] = normalized;
            }
            catch (NormalizationException ex)
            {
                normalizationSkipped++;
                catalogueRowsSkipped++;
                logs.Add(new IngestionLogEntry(
                    IngestionLogSeverity.Warning,
                    IngestionFailureReason.CommercialKeyMissing,
                    ex.Message,
                    new IngestionLocation(0, 0, rowNumber),
                    CapSnippet(ex.RawValue),
                    sourceDocumentId));
            }
        }

        cataloguePrices.AddRange(catalogueByKey.Values);

        var rawManualEntries = new List<RawManualPriceEntry>();
        var manualPrices = new List<IntendedPrice>();
        var manualSubmitted = request.ManualOverrides?.Count ?? 0;
        var manualRejected = 0;

        if (request.ManualOverrides is not null)
        {
            var overrideIndex = 0;
            foreach (var manualRequest in request.ManualOverrides.Take(options.MaxManualOverrides))
            {
                cancellationToken.ThrowIfCancellationRequested();
                overrideIndex++;

                var validation = ManualOverrideValidator.Validate(manualRequest);
                if (!validation.IsValid)
                {
                    manualRejected++;
                    logs.Add(new IngestionLogEntry(
                        IngestionLogSeverity.Error,
                        IngestionFailureReason.ManualOverrideValidationFailed,
                        validation.ErrorMessage ?? "Manual override validation failed.",
                        new IngestionLocation(0, 0, null),
                        CapSnippet(manualRequest.Rrp),
                        sourceDocumentId));
                    continue;
                }

                var rawManual = ManualOverrideValidator.ToRawEntry(
                    manualRequest,
                    $"{sourceDocumentId}/manual-overrides",
                    overrideIndex);
                rawManualEntries.Add(rawManual);

                if (!options.NormalizeOutput)
                {
                    continue;
                }

                try
                {
                    manualPrices.Add(_normalizer.Normalize(rawManual));
                }
                catch (NormalizationException ex)
                {
                    manualRejected++;
                    logs.Add(new IngestionLogEntry(
                        IngestionLogSeverity.Error,
                        IngestionFailureReason.ManualOverrideValidationFailed,
                        ex.Message,
                        new IngestionLocation(0, 0, null),
                        CapSnippet(ex.RawValue),
                        sourceDocumentId));
                }
            }
        }

        var allPrices = cataloguePrices.Concat(manualPrices).ToList();
        var resolvedPrices = new List<IntendedPrice>();
        var resolutionDetails = new List<PricingResolutionDetail>();
        var overrideWinsCount = 0;
        var catalogueOnlyCount = 0;

        var keys = allPrices
            .Select(p => p.Key)
            .Distinct()
            .ToList();

        foreach (var key in keys)
        {
            var resolved = _resolver.Resolve(key, allPrices);
            if (resolved is null)
            {
                continue;
            }

            resolvedPrices.Add(resolved);

            var hadCatalogue = cataloguePrices.Any(p => KeysMatch(p.Key, key));
            var hadManual = manualPrices.Any(p => KeysMatch(p.Key, key));
            if (hadCatalogue && hadManual && resolved.Source == PriceSource.ManualOverride)
            {
                overrideWinsCount++;
            }
            else if (hadCatalogue && !hadManual)
            {
                catalogueOnlyCount++;
            }

            resolutionDetails.Add(new PricingResolutionDetail(
                key,
                resolved.Source,
                resolved.Rrp.Amount,
                hadCatalogue,
                hadManual));
        }

        var status = DetermineStatus(
            rawCatalogueRows.Count,
            catalogueRowsSkipped,
            manualSubmitted,
            manualRejected,
            logs,
            readResult);

        return new RetailPricingCsvIngestionResult
        {
            SourceDocumentId = sourceDocumentId,
            IngestedAt = ingestedAt,
            Status = status,
            RawCatalogueRows = rawCatalogueRows,
            RawManualEntries = rawManualEntries,
            CataloguePrices = cataloguePrices,
            ManualPrices = manualPrices,
            ResolvedPrices = resolvedPrices,
            ResolutionDetails = resolutionDetails,
            LogEntries = logs,
            Summary = new RetailPricingCsvIngestionSummary
            {
                CatalogueRowsRead = catalogueRowsRead,
                CatalogueRowsEmitted = rawCatalogueRows.Count,
                CatalogueRowsSkipped = catalogueRowsSkipped,
                ManualOverridesSubmitted = manualSubmitted,
                ManualOverridesAccepted = rawManualEntries.Count,
                ManualOverridesRejected = manualRejected,
                DuplicateKeyWarnings = duplicateKeyWarnings,
                OverrideWinsCount = overrideWinsCount,
                CatalogueOnlyCount = catalogueOnlyCount,
                ResolvedPriceCount = resolvedPrices.Count,
                NormalizationSkipped = normalizationSkipped
            }
        };
    }

    private static bool KeysMatch(CommercialKey left, CommercialKey right) =>
        left.OfferId.Equals(right.OfferId) &&
        left.SkuId.Equals(right.SkuId) &&
        left.Term == right.Term &&
        left.Frequency == right.Frequency;

    private static IngestionOutcomeStatus DetermineStatus(
        int catalogueRowsEmitted,
        int catalogueRowsSkipped,
        int manualSubmitted,
        int manualRejected,
        IReadOnlyList<IngestionLogEntry> logs,
        ResellerPricingCsvReadResult readResult)
    {
        if (readResult.FileFailure == IngestionFailureReason.MandatoryHeaderMissing)
        {
            return IngestionOutcomeStatus.Failure;
        }

        var anySuccess = catalogueRowsEmitted > 0 || (manualSubmitted - manualRejected) > 0;
        if (!anySuccess && (catalogueRowsSkipped > 0 || manualRejected > 0 || readResult.Rows.Count > 0))
        {
            return IngestionOutcomeStatus.Failure;
        }

        if (catalogueRowsSkipped > 0 || manualRejected > 0 ||
            logs.Any(l => l.Severity == IngestionLogSeverity.Warning))
        {
            return catalogueRowsEmitted > 0 || (manualSubmitted - manualRejected) > 0
                ? IngestionOutcomeStatus.PartialSuccess
                : IngestionOutcomeStatus.Failure;
        }

        return IngestionOutcomeStatus.Success;
    }

    private static RetailPricingCsvIngestionResult FailureResult(
        string sourceDocumentId,
        DateTimeOffset ingestedAt,
        List<IngestionLogEntry> logs,
        IngestionFailureReason reason,
        string message)
    {
        logs.Add(CreateFileLog(IngestionLogSeverity.Error, reason, message, sourceDocumentId));

        return new RetailPricingCsvIngestionResult
        {
            SourceDocumentId = sourceDocumentId,
            IngestedAt = ingestedAt,
            Status = IngestionOutcomeStatus.Failure,
            RawCatalogueRows = [],
            RawManualEntries = [],
            CataloguePrices = [],
            ManualPrices = [],
            ResolvedPrices = [],
            ResolutionDetails = [],
            LogEntries = logs,
            Summary = new RetailPricingCsvIngestionSummary()
        };
    }

    private static IngestionLogEntry CreateRowLog(
        IngestionLogSeverity severity,
        IngestionFailureReason reason,
        string message,
        int rowNumber,
        string sourceDocumentId,
        string? snippet) =>
        new(
            severity,
            reason,
            message,
            new IngestionLocation(0, 0, rowNumber),
            CapSnippet(snippet),
            sourceDocumentId);

    private static IngestionLogEntry CreateFileLog(
        IngestionLogSeverity severity,
        IngestionFailureReason reason,
        string message,
        string sourceDocumentId) =>
        new(severity, reason, message, new IngestionLocation(0, 0, null), null, sourceDocumentId);

    private static string? CapSnippet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 200 ? trimmed : trimmed[..200];
    }
}
