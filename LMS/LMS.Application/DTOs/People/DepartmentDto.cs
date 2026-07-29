namespace LMS.Application.DTOs.People;

/// <summary>
/// Response DTO returned by department endpoints.
/// EmployeeCount reflects the number of active employees currently assigned
/// to this department — computed at query time, not stored.
/// </summary>
public record DepartmentResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    int EmployeeCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Request body for POST /api/v1/departments. FR-21.
/// </summary>
public record CreateDepartmentRequest(
    string Name,
    string? Description);

/// <summary>
/// Request body for PUT /api/v1/departments/{id}. FR-23.
/// All fields optional — only non-null fields are applied (PATCH semantics on a PUT route).
/// </summary>
public record UpdateDepartmentRequest(
    string? Name,
    string? Description,
    bool? IsActive);
