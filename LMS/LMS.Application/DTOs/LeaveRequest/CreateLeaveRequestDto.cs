namespace LMS.Application.DTOs.LeaveRequest;

/// <summary>
/// Payload for POST /api/v1/leave-requests (creates a Draft leave request).
/// </summary>
public class CreateLeaveRequestDto
{
    /// <summary>ID of the leave type to apply for.</summary>
    public Guid LeaveTypeId { get; set; }

    /// <summary>First calendar day of the requested leave period (inclusive).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last calendar day of the requested leave period (inclusive).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Employee-provided reason for the leave.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Optional URL to a supporting document (required for certain leave types).</summary>
    public string? DocumentUrl { get; set; }
}
