namespace LMS.Application.Settings;

/// <summary>
/// Azure AD configuration for SSO (MSAL ConfidentialClientApplication).
/// Values must be supplied via environment variables in production — never commit real secrets.
/// </summary>
public class AzureAdSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}
