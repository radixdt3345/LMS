using LMS.Application.DTOs.People;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>
/// Department management: CRUD with soft-delete and active-list caching.
/// Writes (POST/PUT/DELETE) are HRAdmin/SuperAdmin only — enforced at controller level.
/// FR-21 to FR-26. Tests: UT-14, UT-15, IT-9, IT-10.
/// </summary>
public interface IDepartmentService
{
    /// <summary>
    /// Returns departments. Active-only by default; cached in IMemoryCache for 1 hour.
    /// Pass includeInactive=true to bypass cache and return all. FR-22.
    /// </summary>
    Task<Result<IEnumerable<DepartmentResponse>>> GetDepartmentsAsync(
        bool includeInactive = false, CancellationToken ct = default);

    /// <summary>
    /// Returns a single department by ID. Returns 404 if not found. FR-22.
    /// </summary>
    Task<Result<DepartmentResponse>> GetDepartmentByIdAsync(
        Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new department. Returns 409 if name already exists. FR-21.
    /// Cache is invalidated on success.
    /// </summary>
    Task<Result<DepartmentResponse>> CreateDepartmentAsync(
        CreateDepartmentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates name/description/isActive of a department.
    /// Returns 404 if not found, 409 if new name conflicts. FR-23.
    /// Cache is invalidated on success.
    /// </summary>
    Task<Result<DepartmentResponse>> UpdateDepartmentAsync(
        Guid id, UpdateDepartmentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a department by setting IsActive=false.
    /// Returns 404 if not found, 409 if the department has active employees. FR-24, FR-26.
    /// Idempotent: already-inactive departments return success without re-processing.
    /// Cache is invalidated on success.
    /// </summary>
    Task<Result<bool>> DeleteDepartmentAsync(
        Guid id, CancellationToken ct = default);
}
