namespace LMS.Application.Interfaces;

/// <summary>
/// Abstracts MSAL token exchange to keep AuthService unit-testable without real Azure AD calls.
/// The Infrastructure layer provides the real implementation; tests inject a mock.
/// </summary>
public interface IMsalAuthProvider
{
    /// <summary>
    /// Exchanges an authorization code for the user's Azure AD OID and UPN/email.
    /// Throws on any MSAL failure — caller catches and maps to Result failure.
    /// </summary>
    Task<(string Oid, string Email)> ExchangeCodeAsync(string code, CancellationToken ct = default);
}
