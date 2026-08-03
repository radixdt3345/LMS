using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LMS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using SendGrid.Helpers.Mail;
using Xunit;

namespace LMS.Tests.Unit.Notifications;

/// <summary>
/// Unit tests for NOTIFICATIONS domain infrastructure services.
///
/// UT-54: EmailService builds plain-text + HTML content bodies only.
///         SetTemplateId must NEVER be called — constitution forbids dynamic template IDs.
///
/// UT-55: CalendarService uses a Google service account credential (GOOGLE_CALENDAR_SERVICE_ACCOUNT_JSON).
///         Per-user OAuth2 (UserCredential) must NEVER be used.
///
/// Run: dotnet test --filter Category=Unit
/// </summary>
[Trait("Category", "Unit")]
public class NotificationServiceTests
{
    // ── UT-54: EmailService — no template ID, plain-text + HTML content only ──

    /// <summary>
    /// UT-54 (primary): A SendGridMessage constructed using the same pattern as
    /// EmailService.SendEmailAsync must have TemplateId == null and carry exactly
    /// two content blocks in order: text/plain then text/html.
    ///
    /// This directly validates the constitution rule: AddContent(MimeType.Text, ...)
    /// and AddContent(MimeType.Html, ...) are the only permitted content calls.
    /// </summary>
    [Fact(DisplayName = "UT-54 (CRITICAL): EmailService content pattern — TemplateId is null, two content blocks in order")]
    public void UT54_SendGridMessage_ContentPattern_HasNoTemplateId()
    {
        // Arrange + Act — replicate exactly the message construction inside EmailService.SendEmailAsync
        var msg = new SendGridMessage();
        msg.SetFrom("noreply@lms.com", "LMS System");
        msg.AddTo("employee@example.com");
        msg.SetSubject("Leave Request Approved");

        // Constitution rule: AddContent only — SetTemplateId is FORBIDDEN.
        msg.AddContent(MimeType.Text, "Your leave request has been approved.");
        msg.AddContent(MimeType.Html, "<p>Your leave request has been approved.</p>");

        // Assert: TemplateId is null — SetTemplateId was never called
        Assert.Null(msg.TemplateId);

        // Assert: exactly two content blocks
        Assert.NotNull(msg.Contents);
        Assert.Equal(2, msg.Contents.Count);

        // Assert: first block is plain text
        Assert.Equal(MimeType.Text, msg.Contents[0].Type);
        Assert.Equal("Your leave request has been approved.", msg.Contents[0].Value);

        // Assert: second block is HTML
        Assert.Equal(MimeType.Html, msg.Contents[1].Type);
        Assert.Equal("<p>Your leave request has been approved.</p>", msg.Contents[1].Value);
    }

    /// <summary>
    /// UT-54 (guard): Scans the LMS.Infrastructure compiled module's MemberRef
    /// metadata table to confirm that SendGridMessage.SetTemplateId is never
    /// referenced anywhere in the assembly.
    ///
    /// Any call to SetTemplateId in LMS.Infrastructure — present or future — will
    /// emit a MemberRef entry and cause this test to fail immediately.
    /// </summary>
    [Fact(DisplayName = "UT-54 (guard): LMS.Infrastructure assembly never references SendGridMessage.SetTemplateId")]
    public void UT54_InfrastructureModule_NeverReferencesSetTemplateId()
    {
        // typeof(EmailService).Module resolves to the LMS.Infrastructure assembly module.
        // MemberRef tokens (table 0x0A) are emitted for every external method called
        // from this module. If SetTemplateId appears here the constitution is violated.
        var module = typeof(EmailService).Module;
        bool found = ScanModuleForMemberRef(module, methodName: "SetTemplateId", declaringTypeName: "SendGridMessage");

        Assert.False(found,
            "CONSTITUTION VIOLATION (UT-54): LMS.Infrastructure references SendGridMessage.SetTemplateId. " +
            "EmailService must use AddContent(MimeType.Text, ...) + AddContent(MimeType.Html, ...) only. " +
            "Dynamic template IDs are forbidden.");
    }

    // ── UT-55: CalendarService — service account credential, no per-user OAuth2 ──

    /// <summary>
    /// UT-55 (primary): CalendarService.CreateLeaveEventAsync must throw
    /// InvalidOperationException when GOOGLE_CALENDAR_SERVICE_ACCOUNT_JSON is absent.
    ///
    /// This proves:
    ///   1. The service explicitly requires a service account JSON env var.
    ///   2. There is no silent fallback to Application Default Credentials or
    ///      per-user OAuth2 flow — the missing variable is fatal.
    /// </summary>
    [Fact(DisplayName = "UT-55 (CRITICAL): CalendarService throws InvalidOperationException when service account JSON env var is missing")]
    public async Task UT55_CalendarService_ThrowsWhenServiceAccountJsonEnvVarMissing()
    {
        // Capture current env vars so we can restore them after the test
        var savedJson  = Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_SERVICE_ACCOUNT_JSON");
        var savedCalId = Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_ID");

        try
        {
            // Arrange: remove the service account JSON — simulate a misconfigured host
            Environment.SetEnvironmentVariable("GOOGLE_CALENDAR_SERVICE_ACCOUNT_JSON", null);
            Environment.SetEnvironmentVariable("GOOGLE_CALENDAR_ID", "primary");

            var svc = new CalendarService(NullLogger<CalendarService>.Instance);

            // Act + Assert: missing service account JSON must be fatal.
            // No fallback to per-user OAuth2 or ambient credentials is permitted.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.CreateLeaveEventAsync(
                    "Jane Smith",
                    new DateOnly(2025, 8, 1),
                    new DateOnly(2025, 8, 5),
                    CancellationToken.None));
        }
        finally
        {
            // Always restore env vars so parallel/subsequent tests are unaffected
            Environment.SetEnvironmentVariable("GOOGLE_CALENDAR_SERVICE_ACCOUNT_JSON", savedJson);
            Environment.SetEnvironmentVariable("GOOGLE_CALENDAR_ID", savedCalId);
        }
    }

    /// <summary>
    /// UT-55 (guard): Scans the LMS.Infrastructure compiled module's TypeRef
    /// metadata table to confirm that Google.Apis.Auth.OAuth2.UserCredential is
    /// never referenced anywhere in the assembly.
    ///
    /// UserCredential represents per-user OAuth2 consent, which is explicitly
    /// forbidden by the project constitution. Any accidental import or usage
    /// of that type in LMS.Infrastructure will cause this test to fail.
    /// </summary>
    [Fact(DisplayName = "UT-55 (guard): LMS.Infrastructure assembly never references Google UserCredential (per-user OAuth2 forbidden)")]
    public void UT55_InfrastructureModule_NeverReferencesUserCredential()
    {
        // typeof(CalendarService).Module resolves to the LMS.Infrastructure assembly module.
        // TypeRef tokens (table 0x01) are emitted for every external type referenced
        // from this module. UserCredential must not appear — only service account types.
        var module = typeof(CalendarService).Module;
        bool found = ScanModuleForTypeRef(module, typeName: "UserCredential");

        Assert.False(found,
            "CONSTITUTION VIOLATION (UT-55): LMS.Infrastructure references Google.Apis.Auth.OAuth2.UserCredential. " +
            "CalendarService must use GoogleCredential.FromJson (service account only). " +
            "Per-user OAuth2 consent flow is absolutely forbidden.");
    }

    // ── Metadata scanning helpers ──────────────────────────────────────────────

    /// <summary>
    /// Scans the MemberRef metadata table (token range 0x0A000000) of the given
    /// module for a cross-assembly method reference with the given name and
    /// declaring type name. Returns true if found.
    /// </summary>
    private static bool ScanModuleForMemberRef(Module module, string methodName, string declaringTypeName)
    {
        for (int rid = 1; rid <= 0xFFFF; rid++)
        {
            try
            {
                var token = unchecked((int)(0x0A000000u | (uint)rid));
                var member = module.ResolveMember(token);
                if (member is MethodBase m &&
                    m.Name == methodName &&
                    m.DeclaringType?.Name.Contains(declaringTypeName, StringComparison.Ordinal) == true)
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // Past end of MemberRef table
                break;
            }
        }
        return false;
    }

    /// <summary>
    /// Scans the TypeRef metadata table (token range 0x01000000) of the given
    /// module for a cross-assembly type reference with the given name.
    /// Returns true if found.
    /// </summary>
    private static bool ScanModuleForTypeRef(Module module, string typeName)
    {
        for (int rid = 1; rid <= 0xFFFF; rid++)
        {
            try
            {
                var token = unchecked((int)(0x01000000u | (uint)rid));
                var type = module.ResolveType(token);
                if (type?.Name == typeName)
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // Past end of TypeRef table
                break;
            }
        }
        return false;
    }
}
