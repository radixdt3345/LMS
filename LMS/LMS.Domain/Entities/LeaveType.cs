using LMS.Domain.Enums;

namespace LMS.Domain.Entities;

/// <summary>
/// Leave type definition (Annual, Sick, Casual, Unpaid, Maternity/Paternity).
/// No carry-forward for any type per org policy POL-06 — unused balances lapse at year-end.
/// </summary>
public class LeaveType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? MaxDaysPerYear { get; set; }         // null = unlimited (e.g. Unpaid Leave)
    public AccrualType AccrualType { get; set; } = AccrualType.Annual;
    public bool RequiresDocument { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
