using LMS.Domain.Enums;

namespace LMS.Domain.Entities;

/// <summary>
/// Leave type definition (Annual, Sick, Casual, Unpaid, Maternity/Paternity).
/// Stub entity — LEAVECORE-DB-001 will add full schema.
/// No carry-forward for any type per org policy POL-06.
/// </summary>
public class LeaveType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? MaxDays { get; set; }       // null = unlimited (Unpaid Leave)
    public AccrualType AccrualType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
