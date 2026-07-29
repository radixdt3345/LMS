namespace LMS.Domain.Enums;

/// <summary>
/// Classifies the event that triggered a <see cref="LMS.Domain.Entities.Notification"/>.
/// Stored as smallint in the database (HasConversion&lt;short&gt;()).
/// </summary>
public enum NotificationType
{
    /// <summary>A leave request submitted by the employee was approved.</summary>
    LeaveApproved = 0,

    /// <summary>A leave request submitted by the employee was rejected.</summary>
    LeaveRejected = 1,

    /// <summary>A new leave request was submitted (notifies approvers).</summary>
    LeaveSubmitted = 2,

    /// <summary>A comp-off request was approved and credit was generated.</summary>
    CompOffApproved = 3,

    /// <summary>A comp-off request was rejected.</summary>
    CompOffRejected = 4,

    /// <summary>A system-level alert or administrative message.</summary>
    SystemAlert = 5
}
