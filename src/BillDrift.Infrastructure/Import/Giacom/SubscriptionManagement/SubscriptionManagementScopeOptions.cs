namespace BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement;

/// <summary>
/// Configurable deny and allow token lists for Microsoft 365 / CSP product scope filtering.
/// </summary>
public sealed class SubscriptionManagementScopeOptions
{
    /// <summary>Case-insensitive substring tokens that hard-exclude a row.</summary>
    public IReadOnlyList<string> DenyTokens { get; init; } =
    [
        "Exclaimer",
        "Non-CSP",
        "Non CSP",
        "Third Party",
        "Third-Party",
        "Acronis",
        "Dropbox",
        "Adobe"
    ];

    /// <summary>Service column tokens that hard-include a row.</summary>
    public IReadOnlyList<string> AllowServiceTokens { get; init; } =
    [
        "Microsoft",
        "Office 365",
        "Microsoft 365",
        "M365",
        "CSP"
    ];

    /// <summary>Product type tokens that hard-include a row.</summary>
    public IReadOnlyList<string> AllowProductTypeTokens { get; init; } =
    [
        "CSP",
        "NCE",
        "Microsoft",
        "Online Services"
    ];

    /// <summary>Product name tokens that hard-include a row.</summary>
    public IReadOnlyList<string> AllowProductNameTokens { get; init; } =
    [
        "Microsoft 365",
        "Office 365",
        "Exchange Online",
        "SharePoint",
        "OneDrive",
        "Microsoft Teams",
        "Teams",
        "Defender",
        "Entra",
        "Azure AD",
        "Intune",
        "Power BI",
        "Visio",
        "Project",
        "Dynamics 365 Business",
        "Windows 365"
    ];
}
