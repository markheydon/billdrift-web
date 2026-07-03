using BillDrift.Application.Import;
using BillDrift.Application.Normalization;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Import;
using BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement.Internal;

namespace BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement;

/// <summary>
/// Orchestrates the Giacom Subscription Management CSV ingestion pipeline from file intake through
/// <see cref="MicrosoftSubscriptionLine"/> emission.
/// </summary>
public sealed class SubscriptionManagementCsvIngester : ISubscriptionManagementCsvIngester
{
    private readonly SubscriptionManagementCsvRowReader _rowReader = new();
    private readonly ProductScopeClassifier _scopeClassifier = new();
    private readonly RawSubscriptionManagementRowMapper _mapper = new();
    private readonly ISubscriptionManagementNormalizer _normalizer;

    public SubscriptionManagementCsvIngester(ISubscriptionManagementNormalizer normalizer)
    {
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
    }

    /// <inheritdoc />
    public SubscriptionManagementCsvIngestionResult Ingest(
        SubscriptionManagementCsvIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);

        var options = request.Options ?? new SubscriptionManagementCsvIngestionOptions();
        var ingestedAt = DateTimeOffset.UtcNow;
        var logs = new List<IngestionLogEntry>();

        byte[] bytes;
        using (request.Content)
        {
            try
            {
                bytes = SubscriptionManagementCsvContentReader.ReadBounded(
                    request.Content,
                    options.MaxFileSizeBytes);
            }
            catch (SubscriptionManagementUploadTooLargeException)
            {
                return FailureResult(string.Empty, ingestedAt, request.OriginalFileName, logs,
                    IngestionFailureReason.FileSizeExceeded,
                    $"CSV file exceeds maximum allowed size of {options.MaxFileSizeBytes} bytes.");
            }
        }

        if (bytes.Length == 0)
        {
            return FailureResult(string.Empty, ingestedAt, request.OriginalFileName, logs,
                IngestionFailureReason.EmptyFile, "CSV file is empty.");
        }

        var sourceDocumentId = SubscriptionManagementFileIdentity.ComputeSourceDocumentId(bytes);
        cancellationToken.ThrowIfCancellationRequested();

        var readResult = _rowReader.Read(bytes);
        if (!readResult.IsSuccess)
        {
            logs.Add(CreateFileLog(
                IngestionLogSeverity.Error,
                readResult.FileFailure!.Value,
                readResult.FailureMessage ?? "CSV file could not be read.",
                sourceDocumentId));

            return FailureResult(sourceDocumentId, ingestedAt, request.OriginalFileName, logs,
                readResult.FileFailure.Value,
                readResult.FailureMessage ?? "CSV file could not be read.");
        }

        var rawRows = new List<RawSubscriptionManagementRow>();
        var subscriptionLines = new List<MicrosoftSubscriptionLine>();
        var rowsRead = readResult.Rows.Count;
        var rowsSkipped = 0;
        var rowsExcludedByScope = 0;
        var normalizationSkipped = 0;
        var commercialKeyWarnings = 0;
        var scopeAmbiguityWarnings = 0;

        for (var index = 0; index < readResult.Rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowNumber = index + 1;
            var parsed = SubscriptionManagementCsvHeaderMap.ToParsedRow(
                readResult.Rows[index],
                readResult.FieldMap,
                rowNumber);

            var scope = _scopeClassifier.Classify(parsed);
            switch (scope.Decision)
            {
                case ProductScopeDecision.Exclude:
                    rowsExcludedByScope++;
                    logs.Add(CreateRowLog(
                        IngestionLogSeverity.Warning,
                        scope.Reason ?? IngestionFailureReason.ProductOutOfScope,
                        scope.Message ?? "Row excluded by product scope filter.",
                        rowNumber,
                        sourceDocumentId,
                        GetScopeSnippet(parsed)));
                    continue;

                case ProductScopeDecision.IncludeWithAmbiguityWarning:
                    scopeAmbiguityWarnings++;
                    logs.Add(CreateRowLog(
                        IngestionLogSeverity.Warning,
                        scope.Reason ?? IngestionFailureReason.ProductScopeAmbiguous,
                        scope.Message ?? "Row included with ambiguous product scope.",
                        rowNumber,
                        sourceDocumentId,
                        GetScopeSnippet(parsed)));
                    break;
            }

            WarnOnUnrecognisedFlags(parsed, sourceDocumentId, rowNumber, logs);

            var mapped = _mapper.Map(parsed, sourceDocumentId);
            if (mapped.SkipLog is not null)
            {
                rowsSkipped++;
                logs.Add(mapped.SkipLog);
                continue;
            }

            if (mapped.WarningLog is not null)
            {
                commercialKeyWarnings++;
                logs.Add(mapped.WarningLog);
            }

            if (mapped.Row is null)
            {
                rowsSkipped++;
                continue;
            }

            rawRows.Add(mapped.Row);

            if (!options.NormalizeOutput)
            {
                continue;
            }

            try
            {
                subscriptionLines.Add(_normalizer.Normalize(mapped.Row));
            }
            catch (NormalizationException ex)
            {
                normalizationSkipped++;
                logs.Add(new IngestionLogEntry(
                    IngestionLogSeverity.Warning,
                    IngestionFailureReason.CommercialKeyMissing,
                    ex.Message,
                    new IngestionLocation(0, 0, rowNumber),
                    CapSnippet(ex.RawValue),
                    sourceDocumentId));
            }
        }

        var status = DetermineStatus(rawRows.Count, rowsSkipped, rowsExcludedByScope, logs, readResult);
        return SuccessResult(
            sourceDocumentId,
            ingestedAt,
            request.OriginalFileName,
            rawRows,
            subscriptionLines,
            logs,
            rowsRead,
            rawRows.Count,
            rowsSkipped,
            rowsExcludedByScope,
            normalizationSkipped,
            commercialKeyWarnings,
            scopeAmbiguityWarnings,
            status);
    }

    private static void WarnOnUnrecognisedFlags(
        ParsedSubscriptionManagementRow parsed,
        string sourceDocumentId,
        int rowNumber,
        List<IngestionLogEntry> logs)
    {
        WarnIfUnrecognised(parsed, SubscriptionManagementLogicalField.IsNce, sourceDocumentId, rowNumber, logs);
        WarnIfUnrecognised(parsed, SubscriptionManagementLogicalField.IsTrial, sourceDocumentId, rowNumber, logs);
    }

    private static void WarnIfUnrecognised(
        ParsedSubscriptionManagementRow parsed,
        SubscriptionManagementLogicalField field,
        string sourceDocumentId,
        int rowNumber,
        List<IngestionLogEntry> logs)
    {
        if (!parsed.Fields.TryGetValue(field, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        if (BooleanFlagParser.IsRecognised(raw))
        {
            return;
        }

        logs.Add(CreateRowLog(
            IngestionLogSeverity.Warning,
            IngestionFailureReason.DateUnparseable,
            $"Unrecognised {field} flag value; treated as absent.",
            rowNumber,
            sourceDocumentId,
            raw));
    }

    private static IngestionOutcomeStatus DetermineStatus(
        int rowsEmitted,
        int rowsSkipped,
        int rowsExcludedByScope,
        IReadOnlyList<IngestionLogEntry> logs,
        SubscriptionManagementCsvReadResult readResult)
    {
        if (readResult.FileFailure == IngestionFailureReason.MandatoryHeaderMissing)
        {
            return IngestionOutcomeStatus.Failure;
        }

        if (rowsEmitted == 0 && readResult.Rows.Count > 0)
        {
            return IngestionOutcomeStatus.Failure;
        }

        if (rowsSkipped > 0 || rowsExcludedByScope > 0 ||
            logs.Any(l => l.Severity == IngestionLogSeverity.Warning))
        {
            return rowsEmitted > 0 ? IngestionOutcomeStatus.PartialSuccess : IngestionOutcomeStatus.Failure;
        }

        return IngestionOutcomeStatus.Success;
    }

    private static SubscriptionManagementCsvIngestionResult SuccessResult(
        string sourceDocumentId,
        DateTimeOffset ingestedAt,
        string? originalFileName,
        IReadOnlyList<RawSubscriptionManagementRow> rawRows,
        IReadOnlyList<MicrosoftSubscriptionLine> subscriptionLines,
        IReadOnlyList<IngestionLogEntry> logs,
        int rowsRead,
        int rowsEmitted,
        int rowsSkipped,
        int rowsExcludedByScope,
        int normalizationSkipped,
        int commercialKeyWarnings,
        int scopeAmbiguityWarnings,
        IngestionOutcomeStatus status) =>
        new()
        {
            SourceDocumentId = sourceDocumentId,
            IngestedAt = ingestedAt,
            Status = status,
            RawRows = rawRows,
            SubscriptionLines = subscriptionLines,
            LogEntries = logs,
            Summary = new SubscriptionManagementCsvIngestionSummary
            {
                RowsRead = rowsRead,
                RowsEmitted = rowsEmitted,
                RowsSkipped = rowsSkipped,
                RowsExcludedByScope = rowsExcludedByScope,
                NormalizationSkipped = normalizationSkipped,
                CommercialKeyWarnings = commercialKeyWarnings,
                ScopeAmbiguityWarnings = scopeAmbiguityWarnings
            },
            SourceFile = new SubscriptionManagementSourceFileInfo(sourceDocumentId, originalFileName, rowsRead)
        };

    private static SubscriptionManagementCsvIngestionResult FailureResult(
        string sourceDocumentId,
        DateTimeOffset ingestedAt,
        string? originalFileName,
        List<IngestionLogEntry> logs,
        IngestionFailureReason reason,
        string message)
    {
        logs.Add(CreateFileLog(IngestionLogSeverity.Error, reason, message, sourceDocumentId));

        return new SubscriptionManagementCsvIngestionResult
        {
            SourceDocumentId = sourceDocumentId,
            IngestedAt = ingestedAt,
            Status = IngestionOutcomeStatus.Failure,
            RawRows = [],
            SubscriptionLines = [],
            LogEntries = logs,
            Summary = new SubscriptionManagementCsvIngestionSummary(),
            SourceFile = new SubscriptionManagementSourceFileInfo(sourceDocumentId, originalFileName, 0)
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

    private static string? GetScopeSnippet(ParsedSubscriptionManagementRow parsed)
    {
        parsed.Fields.TryGetValue(SubscriptionManagementLogicalField.ProductName, out var product);
        parsed.Fields.TryGetValue(SubscriptionManagementLogicalField.Service, out var service);
        return CapSnippet(product ?? service);
    }

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
