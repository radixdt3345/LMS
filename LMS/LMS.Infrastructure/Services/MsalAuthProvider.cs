using LMS.Application.Interfaces;
using LMS.Application.Settings;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Production MSAL implementation. Exchanges an authorization code for user identity
/// claims via Azure AD (ConfidentialClientApplication).
/// </summary>
public class MsalAuthProvider : IMsalAuthProvider
{
    private readonly AzureAdSettings _azureAd;

    public MsalAuthProvider(IOptions<AzureAdSettings> azureAd)
    {
        _azureAd = azureAd.Value;
    }

    /// <inheritdoc/>
    public async Task<(string Oid, string Email)> ExchangeCodeAsync(
        string code, CancellationToken ct = default)
    {
        var app = ConfidentialClientApplicationBuilder
            .Create(_azureAd.ClientId)
            .WithClientSecret(_azureAd.ClientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{_azureAd.TenantId}")
            .WithRedirectUri(_azureAd.RedirectUri)
            .Build();

        var result = await app
            .AcquireTokenByAuthorizationCode(new[] { "openid", "profile", "email" }, code)
            .ExecuteAsync(ct);

        var principal = result.ClaimsPrincipal;

        // OID is present as either the short or full claim URI depending on tenant config
        var oid = principal?.FindFirst("oid")?.Value
                  ?? principal?.FindFirst(
                      "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                  ?? throw new InvalidOperationException(
                      "OID claim not found in Azure AD token.");

        var email = result.Account.Username; // UPN — matches User.Email for linking
        return (oid, email);
    }
}
