namespace LMS.Application.DTOs.LeaveRequest;

/// <summary>
/// Read model for a leave request, returned by all LeaveRequestService operations.
/// </summary>
public class LeaveRequestDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Working days computed by SandwichRuleEngine. Zero until submitted.</summary>
    public decimal ComputedDays { get; set; }

    /// <summary>String representation of <see cref="LMS.Domain.Enums.LeaveRequestStatus"/>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>True when StartDate is earlier than the submission date (UTC).</summary>
    public bool IsRetroactive { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string? DocumentUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Approval steps, ordered by StepNumber ascending. Empty until submitted.</summary>
    public List<ApprovalStepDto> ApprovalSteps { get; set; } = new();
}

/// <summary>
/// Read model for a single approval step within a leave request.
/// </summary>
public class ApprovalStepDto
{
    public Guid Id { get; set; }
    public short StepNumber { get; set; }
    public Guid ApproverId { get; set; }

    /// <summary>String representation of <see cref="LMS.Domain.Enums.ApprovalStepStatus"/>.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime? ActedAt { get; set; }
    public string? Comment { get; set; }
}
