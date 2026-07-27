namespace LMS.Domain.Entities;

/// <summary>
/// A public/org holiday that counts as a non-working day.
/// Used by LeaveRequestService to exclude non-working days from leave duration.
/// </summary>
public class Holiday
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int Year { get; set; }
    public bool IsRecurring { get; set; } = false;  // repeats same date every year
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
