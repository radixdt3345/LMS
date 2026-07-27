namespace LMS.Domain.Entities;

/// <summary>
/// Organisation department. Stub entity — PEOPLE-DB-001 will add full schema.
/// </summary>
public class Department
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
