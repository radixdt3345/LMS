namespace LMS.Application.DTOs.People;

/// <summary>Read model returned by department endpoints.</summary>
public record DepartmentDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Payload for POST /api/v1/departments.</summary>
public record CreateDepartmentDto(string Name, string? Description);

/// <summary>Payload for PUT /api/v1/departments/{id}.</summary>
public record UpdateDepartmentDto(string Name, string? Description);
