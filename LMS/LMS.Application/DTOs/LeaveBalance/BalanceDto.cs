namespace LMS.Application.DTOs.LeaveBalance;

/// <summary>
/// Leave balance summary returned to callers for a single leave type / year.
/// </summary>
public class BalanceDto
{
    /// <summary>Leave type identifier.</summary>
    public Guid LeaveTypeId { get; set; }

    /// <summary>Human-readable leave type name (e.g. "Annual Leave").</summary>
    public string LeaveTypeName { get; set; } = string.Empty;

    /// <summary>Total days allocated for the year.</summary>
    public decimal AllocatedDays { get; set; }

    /// <summary>Days already consumed by approved requests.</summary>
    public decimal UsedDays { get; set; }

    /// <summary>Remaining days available (AllocatedDays - UsedDays). Never negative.</summary>
    public decimal AvailableDays { get; set; }

    /// <summary>Calendar year this balance applies to.</summary>
    public short Year { get; set; }
}