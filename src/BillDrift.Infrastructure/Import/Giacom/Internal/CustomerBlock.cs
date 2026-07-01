namespace BillDrift.Infrastructure.Import.Giacom.Internal;

public sealed record CustomerBlock(
    int BlockIndex,
    int PageNumber,
    string? CustomerNameRaw,
    string? MexIdRaw,
    IReadOnlyList<ParsedProductLine> ProductLines);
