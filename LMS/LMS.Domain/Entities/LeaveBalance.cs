namespace LMS.Domain.Entities;

/// <summary>
/// Tracks leave balance per employee per leave type per year.
/// No carry_forward_days accumulation logic here — the column exists for
/// recording a starting carried balance set by HR Admin only (org policy POL-06
/// forbids automatic carry-forward; this field is set to 0 by default).
/// </summary>
public class LeaveBalance
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid LeaveTypeId { get; set; }

    /// <summary>Calendar year this balance applies to (e.g. 2025).</summary>
    public short Year { get; set; }

    /// <summary>Total days allocated for the year (includes any manual carry-in).</summary>
    public decimal AllocatedDays { get; set; } = 0m;

    /// <summary>Days consumed by approved leave requests.</summary>
    public decimal UsedDays { get; set; } = 0m;

    /// <summary>
    /// Carried-forward days set by HR Admin at year-start.
    /// Always 0 for standard leave types per POL-06; held for future policy changes.
    /// </summary>
    public decimal CarriedForwardDays { get; set; } = 0m;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public LeaveType LeaveType { get; set; } = null!;
}
