namespace LMS.Domain.Entities;

/// <summary>
/// Tracks a user's leave balance for a specific leave type and calendar year.
/// No carry-forward field — per POL-06 / FR-30, unused balances lapse at year-end.
/// </summary>
public class LeaveBalance
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public int Year { get; set; }
    public decimal Balance { get; set; }     // days remaining
    public decimal Used { get; set; }        // days consumed this year
    public decimal Allocated { get; set; }   // days granted for the year
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public LeaveType LeaveType { get; set; } = null!;
}
