using BillDrift.Application.Ingestion;

namespace BillDrift.Application.Import.Giacom;

/// <summary>Orchestrates Giacom billing PDF upload, parsing, normalization, and persistence.</summary>
public interface IGiacomPdfIngestionService
{
    /// <summary>Ingests a PDF stream and persists normalized supplier cost lines.</summary>
    Task<GiacomPdfIngestionRun> IngestAndPersistAsync(
        Stream pdfContent,
        string? originalFileName,
        CancellationToken cancellationToken = default);
}
