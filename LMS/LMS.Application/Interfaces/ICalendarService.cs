using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>
/// Company-wide Google Calendar operations via a service account.
/// Never initiates per-user OAuth2 consent flows.
/// </summary>
public interface ICalendarService
{
    /// <summary>
    /// Creates an all-day leave event on the shared company calendar.
    /// Returns the Google Calendar event ID (used to delete it later).
    /// </summary>
    Task<Result<string?>> CreateLeaveEventAsync(
        string employeeName,
        DateOnly start,
        DateOnly end,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a previously created leave event by its Google Calendar event ID.
    /// </summary>
    Task<Result<bool>> DeleteLeaveEventAsync(string eventId, CancellationToken ct = default);
}
