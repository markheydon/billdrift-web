namespace BillDrift.Domain.Common;

public enum ProductClassification
{
    Csp,
    NonCsp
}

public enum MappingConfidence
{
    High,
    Medium,
    Low,
    Unmapped
}

public enum MappingSource
{
    Manual,
    Imported,
    Inferred
}

public enum MatchConfidence
{
    High,
    Medium,
    Low,
    None
}
