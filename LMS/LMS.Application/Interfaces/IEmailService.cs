using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>
/// Email delivery abstraction.
/// Implementation must use plain-text + inline-HTML only (no SendGrid template IDs).
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a transactional email.
    /// Both <paramref name="textBody"/> and <paramref name="htmlBody"/> are required;
    /// the implementation adds them as MIME parts in that order (text first, then HTML).
    /// </summary>
    Task<Result<bool>> SendEmailAsync(
        string toEmail,
        string subject,
        string textBody,
        string htmlBody,
        CancellationToken ct = default);
}
