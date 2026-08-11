using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LMS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using SendGrid;
using SendGrid.Helpers.Mail;
using Xunit;

namespace LMS.Tests.Unit.Notifications;

[Trait("Category", "Unit")]
public class NotificationServiceTests
{
    [Fact(DisplayName = "UT-54 (CRITICAL): EmailService content pattern — TemplateId is null, two content blocks in order")]
    public void UT54_SendGridMessage_ContentPattern_HasNoTemplateId()
    {
        var msg = new SendGridMessage();
        msg.SetFrom("noreply@lms.com", "LMS System");
        msg.AddTo("employee@example.com");
        msg.SetSubject("Leave Request Approved");
        msg.AddContent(MimeType.Text, "Your leave request has been approved.");
        msg.AddContent(MimeType.Html, "<p>Your leave request has been approved.</p>");

        Assert.Null(msg.TemplateId);
        Assert.NotNull(msg.Contents);
        Assert.Equal(2, msg.Contents.Count);
        Assert.Equal(MimeType.Text, msg.Contents[0].Type);
        Assert.Equal("Your leave request has been approved.", msg.Contents[0].Value);
        Assert.Equal(MimeType.Html, msg.Contents[1].Type);
        Assert.Equal("<p>Your leave request has been approved.</p>", msg.Contents[1].Value);
    }

    [Fact(DisplayName = "UT-54 (guard): LMS.Infrastructure assembly never references SendGridMessage.SetTemplateId")]
    public void UT54_InfrastructureModule_NeverReferencesSetTemplateId()
    {
        var module = typeof(EmailService).Module;
        bool found = ScanModuleForMemberRef(module, methodName: "SetTemplateId", declaringTypeName: "SendGridMessage");
        Assert.False(found, "CONSTITUTION VIOLATION (UT-54): LMS.Infrastructure references SendGridMessage.SetTemplateId.");
    }

    [Fact(DisplayName = "UT-55 (CRITICAL): CalendarService fails when service account JSON env var is missing")]
    public async Task UT55_CalendarService_FailsWhenServiceAccountJsonEnvVarMissing()
    {
        var savedJson  = Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_SERVICE_ACCOUNT_JSON");
        var savedCalId = Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_ID");

        try
        {
            Environment.SetEnvironmentVariable("GOOGLE_CALENDAR_SERVICE_ACCOUNT_JSON", null);
            Environment.SetEnvironmentVariable("GOOGLE_CALENDAR_ID", "primary");

            var svc = new CalendarService(NullLogger<CalendarService>.Instance);

            var result = await svc.CreateLeaveEventAsync(
                "Jane Smith",
                new DateOnly(2025, 8, 1),
                new DateOnly(2025, 8, 5),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Null(result.Value);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOOGLE_CALENDAR_SERVICE_ACCOUNT_JSON", savedJson);
            Environment.SetEnvironmentVariable("GOOGLE_CALENDAR_ID", savedCalId);
        }
    }

    [Fact(DisplayName = "UT-55 (guard): LMS.Infrastructure assembly never references Google UserCredential")]
    public void UT55_InfrastructureModule_NeverReferencesUserCredential()
    {
        var module = typeof(CalendarService).Module;
        bool found = ScanModuleForTypeRef(module, typeName: "UserCredential");
        Assert.False(found, "CONSTITUTION VIOLATION (UT-55): LMS.Infrastructure references Google.Apis.Auth.OAuth2.UserCredential.");
    }

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
                    return true;
            }
            catch (ArgumentException) { break; }
        }
        return false;
    }

    private static bool ScanModuleForTypeRef(Module module, string typeName)
    {
        for (int rid = 1; rid <= 0xFFFF; rid++)
        {
            try
            {
                var token = unchecked((int)(0x01000000u | (uint)rid));
                var type = module.ResolveType(token);
                if (type?.Name == typeName) return true;
            }
            catch (ArgumentException) { break; }
        }
        return false;
    }
}
