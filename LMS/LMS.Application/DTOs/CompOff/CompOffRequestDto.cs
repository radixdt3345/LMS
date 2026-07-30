namespace LMS.Application.DTOs.CompOff;

/// <summary>
/// Read model for a comp-off request — returned by all CompOff endpoints.
/// </summary>
public class CompOffRequestDto
{
    public Guid    Id          { get; set; }
    public Guid    EmployeeId  { get; set; }
    public DateOnly WorkedDate  { get; set; }
    public decimal WorkedHours { get; set; }
    /// <summary>String representation of CompOffStatus enum (Pending/Approved/Rejected).</summary>
    public string  Status      { get; set; } = string.Empty;
    public DateTime CreatedAt  { get; set; }
    public DateTime UpdatedAt  { get; set; }
}
