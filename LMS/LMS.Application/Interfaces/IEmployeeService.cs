using LMS.Application.DTOs.People;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>
/// Employee management service interface. FR-12 to FR-20.
/// All methods return Result&lt;T&gt; — never throw for expected failures.
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
    /// Calls DeriveRole on the assigned manager (if any) after creation.
    /// </summary>
    Task<Result<EmployeeDto>> CreateEmployeeAsync(
        CreateEmployeeDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Patches employee profile fields (patch semantics — null fields are left unchanged). FR-14.
    /// When ManagerId changes, calls DeriveRole on both old and new manager.
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

    /// <summary>
    /// Returns the active direct reports of the specified manager. FR-20.
    /// Caller must be the manager themselves or HRAdmin/SuperAdmin; returns 403 otherwise.
    /// </summary>
    Task<Result<IEnumerable<EmployeeDto>>> GetTeamAsync(
        Guid managerId,
        Guid callerId,
        bool callerIsHrAdmin,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the authenticated user's own employee profile. FR-12.
    /// </summary>
    Task<Result<EmployeeDto>> GetMyProfileAsync(
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the authenticated user's own profile (firstName, lastName, phone only). FR-14.
    /// Role, EmployeeCode, DepartmentId, and ManagerId are silently ignored via this endpoint.
    /// </summary>
    Task<Result<EmployeeDto>> UpdateMyProfileAsync(
        Guid userId,
        UpdateMyProfileDto dto,
        CancellationToken ct = default);
}
