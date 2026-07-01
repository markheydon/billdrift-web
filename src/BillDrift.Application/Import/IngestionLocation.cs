namespace BillDrift.Application.Import;

public sealed record IngestionLocation(
    int PageNumber,
    int BlockIndex,
    int? LineIndex);
