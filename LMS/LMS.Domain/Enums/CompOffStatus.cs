namespace LMS.Domain.Enums;

/// <summary>
/// Lifecycle status of a comp-off request.
/// Stored as smallint in the database (HasConversion&lt;short&gt;()).
/// </summary>
public enum CompOffStatus
{
    /// <summary>Awaiting manager or HR-admin approval.</summary>
    Pending = 0,

    /// <summary>Request approved; comp-off credit has been generated.</summary>
    Approved = 1,

    /// <summary>Request rejected; no credit generated.</summary>
    Rejected = 2
}
