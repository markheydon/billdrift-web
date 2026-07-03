namespace BillDrift.Application.CatalogueReconciliation.Stages;

/// <summary>Validates catalogue reconciliation inputs before indexing.</summary>
public sealed class ValidateInputsStage : ICatalogueReconciliationStage
{
    /// <inheritdoc />
    public void Execute(CatalogueReconciliationContext context)
    {
        if (context.Inputs.StripeProducts.Count == 0 && context.Inputs.StripePrices.Count == 0)
        {
            context.ValidationError = "Stripe catalogue snapshot is empty.";
        }

        if (context.Inputs.ProductMappings is null)
        {
            context.ValidationError = "Product mappings are required.";
        }

        if (context.Inputs.IntendedPrices is null)
        {
            context.ValidationError = "Intended prices are required.";
        }
    }
}
