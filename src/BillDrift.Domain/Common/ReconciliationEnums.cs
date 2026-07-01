namespace BillDrift.Domain.Common;

public enum MismatchType
{
    MissingInStripe,
    QuantityMismatch,
    BillingFrequencyMismatch,
    PriceMismatch,
    CatalogueMissing,
    MappingMissing,
    MappingAmbiguous
}

public enum MismatchSeverity
{
    Info,
    Warning,
    Error
}

public enum ProposedActionType
{
    UpdateQuantity,
    SwitchPrice,
    CreateMissingItem,
    CreateOrUpdateCatalogueEntry
}
