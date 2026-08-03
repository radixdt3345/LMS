using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using Microsoft.Extensions.Logging;

// Alias to avoid name collision between this class and Google's CalendarService type.
using GoogleCalendarService = Google.Apis.Calendar.v3.CalendarService;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Manages all-day leave events on the shared company Google Calendar.
/// CONSTITUTION RULE: service account credentials only.
/// NO per-user OAuth2 consent flow — ever.
/// Env vars required:
///   GOOGLE_CALENDAR_SERVICE_ACCOUNT_JSON — full JSON of the service account key file.
///   GOOGLE_CALENDAR_ID                   — target calendar ID (e.g. "primary" or a shared calendar address).
/// </summary>
public class CalendarService : ICalendarService
{
    private const string CalendarScope = "https://www.googleapis.com/auth/calendar";
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(ILogger<CalendarService> logger)
    {
        _logger = logger;
    }

    // ── Private factory ───────────────────────────────────────────────────────

    private static GoogleCalendarService BuildGoogleCalendarService()
    {
        var json = Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_SERVICE_ACCOUNT_JSON")
            ?? throw new InvalidOperationException(
                "GOOGLE_CALENDAR_SERVICE_ACCOUNT_JSON environment variable is not set.");

        var credential = GoogleCredential
            .FromJson(json)
            .CreateScoped(CalendarScope);

        return new GoogleCalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "LMS Calendar Service"
        });
    }

    private static string GetCalendarId() =>
        Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_ID")
            ?? throw new InvalidOperationException(
                "GOOGLE_CALENDAR_ID environment variable is not set.");

    // ── Public operations ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Google Calendar all-day events use inclusive start and exclusive end dates.
    /// We add 1 day to <paramref name="end"/> so the event covers the full last day.
    /// </remarks>
    public async Task<Result<string?>> CreateLeaveEventAsync(
        string employeeName,
        DateOnly start,
        DateOnly end,
        CancellationToken ct = default)
    {
        try
        {
            var calendarId = GetCalendarId();
            var svc = BuildGoogleCalendarService();

            var ev = new Event
            {
                Summary = $"{employeeName} - Leave",
                Start = new EventDateTime { Date = start.ToString("yyyy-MM-dd") },
                // Google Calendar end date is exclusive — add 1 day to include the last day.
                End = new EventDateTime { Date = end.AddDays(1).ToString("yyyy-MM-dd") }
            };

            var created = await svc.Events.Insert(ev, calendarId).ExecuteAsync(ct);

            _logger.LogInformation(
                "Calendar event {EventId} created for {Employee} ({Start} to {End})",
                created.Id, employeeName, start, end);

            return Result<string?>.Success(created.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create calendar event for {Employee}", employeeName);
            return Result<string?>.Failure("Calendar event creation failed.", 500);
        }
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> DeleteLeaveEventAsync(
        string eventId,
        CancellationToken ct = default)
    {
        try
        {
            var calendarId = GetCalendarId();
            var svc = BuildGoogleCalendarService();

            await svc.Events.Delete(calendarId, eventId).ExecuteAsync(ct);

            _logger.LogInformation("Calendar event {EventId} deleted", eventId);
            return Result<bool>.Success(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete calendar event {EventId}", eventId);
            return Result<bool>.Failure("Calendar event deletion failed.", 500);
        }
    }
}
