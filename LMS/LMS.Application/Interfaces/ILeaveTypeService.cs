using LMS.Application.DTOs.LeaveCore;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>
/// Leave type management: CRUD for leave type definitions.
/// Writes are SuperAdmin only — enforced at the controller level.
/// FR-27 to FR-30. No carry_forward — POL-06.
/// </summary>
public interface ILeaveTypeService
{
    /// <summary>
    /// Returns all leave types. Active-only by default; pass includeInactive=true for all.
    /// </summary>
    Task<Result<IEnumerable<LeaveTypeDto>>> GetLeaveTypesAsync(
        bool includeInactive = false, CancellationToken ct = default);

    /// <summary>
    /// Returns a single leave type by ID. Returns 404 if not found.
    /// </summary>
    Task<Result<LeaveTypeDto>> GetLeaveTypeByIdAsync(
        Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new leave type. Name must be unique among active types.
    /// MaxDaysPerYear=null represents unlimited (e.g. Unpaid Leave). FR-27.
    /// </summary>
    Task<Result<LeaveTypeDto>> CreateLeaveTypeAsync(
        CreateLeaveTypeDto dto, CancellationToken ct = default);

    /// <summary>
    /// Applies partial updates to a leave type. Returns 404 if not found. FR-28.
    /// </summary>
    Task<Result<LeaveTypeDto>> UpdateLeaveTypeAsync(
        Guid id, UpdateLeaveTypeDto dto, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a leave type by setting IsActive=false. Idempotent. FR-29.
    /// Returns 404 if not found.
    /// </summary>
    Task<Result<bool>> DeactivateLeaveTypeAsync(
        Guid id, CancellationToken ct = default);
}
