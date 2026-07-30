namespace LMS.Application.DTOs.CompOff;

/// <summary>
/// Input payload for POST /api/v1/comp-off/requests.
/// </summary>
public class CreateCompOffRequestDto
{
    /// <summary>
    /// Calendar date on which extra work was performed.
    /// Must NOT be a regular working day (checked against HolidayService).
    /// </summary>
    public DateOnly WorkedDate  { get; set; }

    /// <summary>
    /// Hours worked on that date. Must be >= 4 (422 otherwise).
    /// 4 h yields 0.5 comp-off day; 8 h yields 1.0 day.
    /// </summary>
    public decimal WorkedHours { get; set; }
}
