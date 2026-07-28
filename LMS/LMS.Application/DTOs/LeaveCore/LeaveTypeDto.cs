using LMS.Domain.Enums;

namespace LMS.Application.DTOs.LeaveCore;

/// <summary>
/// Read model for a leave type returned to API callers.
/// </summary>
public class LeaveTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? MaxDaysPerYear { get; set; }
    public AccrualType AccrualType { get; set; }
    public bool RequiresDocument { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Write model for creating a new leave type. SuperAdmin only.
/// No carry_forward field — prohibited by org policy POL-06/FR-30.
/// </summary>
public class CreateLeaveTypeDto
{
    public string Name { get; set; } = string.Empty;
    public int? MaxDaysPerYear { get; set; }    // null = unlimited (e.g. Unpaid Leave)
    public AccrualType AccrualType { get; set; } = AccrualType.Annual;
    public bool RequiresDocument { get; set; } = false;
}

/// <summary>
/// Write model for updating an existing leave type. SuperAdmin only.
/// All fields optional — only non-null values are applied.
/// </summary>
public class UpdateLeaveTypeDto
{
    public string? Name { get; set; }
    public int? MaxDaysPerYear { get; set; }
    public AccrualType? AccrualType { get; set; }
    public bool? RequiresDocument { get; set; }
}
