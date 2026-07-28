using LMS.Application.DTOs.People;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>
/// Employee management service interface. FR-12 to FR-20.
/// All methods return Result<T> — never throw for expected failures.
/// </summary>
public interface IEmployeeService
{
    /// <summary>
    /// Returns a paginated list of active employees. FR-12.
    /// Optionally filtered by department or free-text search (email, first/last name, employee code).
    /// </summary>
    Task<Result<PagedResult<EmployeeDto>>> GetEmployeesAsync(
        int page,
        int limit,
        Guid? deptId,
        string? search,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a single employee by their ID. Returns 404 if not found. FR-12.
    /// </summary>
    Task<Result<EmployeeDto>> GetEmployeeByIdAsync(
        Guid id,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new employee (User record). Returns 409 if email already exists. FR-13.
    /// Password is BCrypt-hashed when provided; AzureAdOid enables SSO-only accounts.
    /// </summary>
    Task<Result<EmployeeDto>> CreateEmployeeAsync(
        CreateEmployeeDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Patches employee profile fields (patch semantics — null fields are left unchanged). FR-14.
    /// Calls AuditService.LogAsync on every invocation.
    /// </summary>
    Task<Result<EmployeeDto>> UpdateEmployeeAsync(
        Guid id,
        UpdateEmployeeDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes an employee by setting IsActive = false. Idempotent. FR-15.
    /// Calls AuditService.LogAsync when the state actually changes.
    /// </summary>
    Task<Result<bool>> DeactivateEmployeeAsync(
        Guid id,
        CancellationToken ct = default);
}
