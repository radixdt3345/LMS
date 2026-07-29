using LMS.Application.Common;
using LMS.Application.DTOs.People;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>
/// CRUD operations for departments.
/// All list results are cached by IMemoryCache.
/// </summary>
public interface IDepartmentService
{
    Task<Result<PaginatedResult<DepartmentDto>>> GetAllAsync(
        int page, int limit, CancellationToken ct = default);

    Task<Result<DepartmentDto>> GetByIdAsync(
        Guid id, CancellationToken ct = default);

    Task<Result<DepartmentDto>> CreateAsync(
        CreateDepartmentDto dto, CancellationToken ct = default);

    Task<Result<DepartmentDto>> UpdateAsync(
        Guid id, UpdateDepartmentDto dto, CancellationToken ct = default);

    /// <summary>Soft-delete: sets IsActive = false.</summary>
    Task<Result<bool>> DeleteAsync(
        Guid id, CancellationToken ct = default);
}
