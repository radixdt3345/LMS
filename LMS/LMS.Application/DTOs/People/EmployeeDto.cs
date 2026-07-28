namespace LMS.Application.DTOs.People;

/// <summary>
/// Read DTO returned by all employee endpoints. FR-12.
/// Role is the string name of the UserRole enum.
/// </summary>
public record EmployeeDto(
    Guid Id,
    string Email,
    string Role,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? ManagerId,
    bool IsActive,
    DateTime CreatedAt
);

/// <summary>
/// Payload for creating a new employee. FR-13.
/// Password and AzureAdOid are mutually optional — provide at least one auth method.
/// Role defaults to Employee when omitted.
/// </summary>
public record CreateEmployeeDto(
    string Email,
    string? Password,
    string? AzureAdOid,
    Guid? DepartmentId,
    Guid? ManagerId,
    string? Role
);

/// <summary>
/// Patch payload for updating an employee. FR-14.
/// All fields are nullable — null means "leave unchanged".
/// </summary>
public record UpdateEmployeeDto(
    string? FirstName,
    string? LastName,
    string? Phone,
    string? EmployeeCode,
    Guid? DepartmentId,
    Guid? ManagerId,
    string? Role,
    bool? IsActive
);
