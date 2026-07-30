namespace LMS.Application.DTOs.CompOff;

/// <summary>
/// Read model for a comp-off credit record — returned by GET /comp-off/credits/me
/// and CreditBalanceAsync.
/// </summary>
public class CompOffCreditDto
{
    public Guid    Id               { get; set; }
    public Guid    EmployeeId       { get; set; }
    public Guid    CompOffRequestId { get; set; }
    /// <summary>Credit days granted: 0.5 or 1.0.</summary>
    public decimal CreditDays       { get; set; }
    /// <summary>Expiry date = worked_date + 180 days.</summary>
    public DateOnly ExpiresAt       { get; set; }
    public decimal UsedDays         { get; set; }
    public DateTime CreatedAt       { get; set; }
}
