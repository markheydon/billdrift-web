using BillDrift.Domain.Billing;
using BillDrift.Domain.Import;

namespace BillDrift.Application.Normalization;

public interface IGiacomBillingNormalizer
{
    SupplierCostLine Normalize(RawGiacomBillingLine raw);
}

public interface ISubscriptionManagementNormalizer
{
    MicrosoftSubscriptionLine Normalize(RawSubscriptionManagementRow raw);
}

public interface IPriceListNormalizer
{
    IntendedPrice Normalize(RawPriceListRow raw);
    IntendedPrice Normalize(RawManualPriceEntry raw);
}
