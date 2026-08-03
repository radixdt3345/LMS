using LMS.Application.Interfaces;
using LMS.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Sends transactional email via SendGrid v3 REST API.
/// CONSTITUTION RULE: plain-text + inline-HTML content only.
/// SetTemplateId is FORBIDDEN — no dynamic template IDs anywhere.
/// Config keys: SendGrid:ApiKey, SendGrid:SenderEmail, SendGrid:SenderName.
/// </summary>
public class EmailService : IEmailService
{
    private readonly string _apiKey;
    private readonly string _senderEmail;
    private readonly string _senderName;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _logger = logger;

        _apiKey = configuration["SendGrid:ApiKey"]
            ?? throw new InvalidOperationException(
                "SendGrid:ApiKey is not configured. Set it via appsettings or an environment variable.");

        _senderEmail = configuration["SendGrid:SenderEmail"] ?? "noreply@lms.com";
        _senderName = configuration["SendGrid:SenderName"] ?? "LMS System";
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> SendEmailAsync(
        string toEmail,
        string subject,
        string textBody,
        string htmlBody,
        CancellationToken ct = default)
    {
        try
        {
            var client = new SendGridClient(_apiKey);

            var msg = new SendGridMessage();
            msg.SetFrom(_senderEmail, _senderName);
            msg.AddTo(toEmail);
            msg.SetSubject(subject);

            // Plain-text first (required), then HTML.
            // NO SetTemplateId — forbidden by constitution.
            msg.AddContent(MimeType.Text, textBody);
            msg.AddContent(MimeType.Html, htmlBody);

            var response = await client.SendEmailAsync(msg, ct);
            var statusCode = (int)response.StatusCode;

            if (statusCode is >= 200 and < 300)
            {
                _logger.LogInformation(
                    "Email sent to {ToEmail} — subject: {Subject}", toEmail, subject);
                return Result<bool>.Success(true);
            }

            var responseBody = await response.Body.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "SendGrid returned {StatusCode} for {ToEmail}: {Body}",
                statusCode, toEmail, responseBody);

            return Result<bool>.Failure($"SendGrid error {statusCode}.", 502);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail}", toEmail);
            return Result<bool>.Failure("Email delivery failed due to an unexpected error.", 500);
        }
    }
}
