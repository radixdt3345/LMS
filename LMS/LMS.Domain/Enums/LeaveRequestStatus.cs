namespace LMS.Domain.Enums;

/// <summary>
/// Status values for a leave request lifecycle.
/// Stored as smallint in the database.
/// </summary>
public enum LeaveRequestStatus : short
{
    /// <summary>Request created but not yet submitted for approval.</summary>
    Draft = 0,

    /// <summary>Request submitted and awaiting approver action.</summary>
    Pending = 1,

    /// <summary>All approval steps have been approved.</summary>
    Approved = 2,

    /// <summary>Request was rejected by an approver.</summary>
    Rejected = 3,

    /// <summary>Request was cancelled by the employee before a decision.</summary>
    Cancelled = 4,

    /// <summary>Previously approved leave was revoked by HR Admin.</summary>
    Revoked = 5
}
