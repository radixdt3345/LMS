namespace LMS.Application.DTOs.LeaveCore;

/// <summary>
/// Read model for a public/org holiday.
/// </summary>
public class HolidayDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int Year { get; set; }
    public bool IsRecurring { get; set; }
}

/// <summary>
/// Payload for creating a new holiday.
/// Year is derived from Date.Year — not supplied separately.
/// </summary>
public class CreateHolidayDto
{
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public bool IsRecurring { get; set; } = false;
}

/// <summary>
/// Partial-update payload for an existing holiday.
/// Null fields are ignored (patch semantics).
/// </summary>
public class UpdateHolidayDto
{
    public string? Name { get; set; }
    public DateOnly? Date { get; set; }
    public bool? IsRecurring { get; set; }
}
