namespace LMS.Domain.Enums;

/// <summary>
/// Status values for a single approval step within a leave request.
/// Stored as smallint in the database.
/// </summary>
public enum ApprovalStepStatus : short
{
    /// <summary>Step is awaiting the approver's action.</summary>
    Pending = 0,

    /// <summary>Approver has approved this step.</summary>
    Approved = 1,

    /// <summary>Approver has rejected this step.</summary>
    Rejected = 2
}
