using LMS.Application.DTOs.People;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Department CRUD — FR-21 to FR-26.
/// The active department list is cached in IMemoryCache with a 1-hour TTL.
/// Every mutation (create/update/delete) evicts the cache so the next GET re-fetches.
/// AuditService integration is deferred to the REPORTING domain issue.
/// </summary>
public class DepartmentService : IDepartmentService
{
    private readonly LmsDbContext _db;
    private readonly IMemoryCache _cache;
    private const string ActiveCacheKey = "departments_active";

    public DepartmentService(LmsDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <inheritdoc/>
    public async Task<Result<IEnumerable<DepartmentResponse>>> GetDepartmentsAsync(
        bool includeInactive = false, CancellationToken ct = default)
    {
        // Only the active list is served from cache
        if (!includeInactive
            && _cache.TryGetValue(ActiveCacheKey, out IEnumerable<DepartmentResponse>? cached))
        {
            return Result<IEnumerable<DepartmentResponse>>.Success(cached!);
        }

        var query = _db.Departments.AsNoTracking();
        if (!includeInactive)
            query = query.Where(d => d.IsActive);

        var items = await query
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentResponse(
                d.Id, d.Name, d.Description, d.IsActive, d.CreatedAt, d.UpdatedAt))
            .ToListAsync(ct);

        if (!includeInactive)
        {
            _cache.Set(ActiveCacheKey, (IEnumerable<DepartmentResponse>)items,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });
        }

        return Result<IEnumerable<DepartmentResponse>>.Success(items);
    }

    /// <inheritdoc/>
    public async Task<Result<DepartmentResponse>> GetDepartmentByIdAsync(
        Guid id, CancellationToken ct = default)
    {
        var dept = await _db.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (dept is null)
            return Result<DepartmentResponse>.Failure("Department not found.", 404);

        return Result<DepartmentResponse>.Success(MapToResponse(dept));
    }

    /// <inheritdoc/>
    public async Task<Result<DepartmentResponse>> CreateDepartmentAsync(
        CreateDepartmentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<DepartmentResponse>.Failure("Department name is required.", 400);

        var duplicate = await _db.Departments
            .AnyAsync(d => d.Name == request.Name.Trim() && d.IsActive, ct);

        if (duplicate)
            return Result<DepartmentResponse>.Failure(
                $"A department named '{request.Name}' already exists.", 409);

        var now = DateTime.UtcNow;
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Departments.Add(dept);
        await _db.SaveChangesAsync(ct);

        _cache.Remove(ActiveCacheKey);

        return Result<DepartmentResponse>.Success(MapToResponse(dept));
    }

    /// <inheritdoc/>
    public async Task<Result<DepartmentResponse>> UpdateDepartmentAsync(
        Guid id, UpdateDepartmentRequest request, CancellationToken ct = default)
    {
        var dept = await _db.Departments.FindAsync(new object[] { id }, ct);

        if (dept is null)
            return Result<DepartmentResponse>.Failure("Department not found.", 404);

        if (request.Name is not null)
        {
            var trimmed = request.Name.Trim();
            if (trimmed != dept.Name)
            {
                var nameConflict = await _db.Departments
                    .AnyAsync(d => d.Name == trimmed && d.Id != id, ct);

                if (nameConflict)
                    return Result<DepartmentResponse>.Failure(
                        $"A department named '{request.Name}' already exists.", 409);

                dept.Name = trimmed;
            }
        }

        if (request.Description is not null)
            dept.Description = request.Description.Trim();

        if (request.IsActive is not null)
            dept.IsActive = request.IsActive.Value;

        dept.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _cache.Remove(ActiveCacheKey);

        return Result<DepartmentResponse>.Success(MapToResponse(dept));
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> DeleteDepartmentAsync(
        Guid id, CancellationToken ct = default)
    {
        var dept = await _db.Departments.FindAsync(new object[] { id }, ct);

        if (dept is null)
            return Result<bool>.Failure("Department not found.", 404);

        if (!dept.IsActive)
            return Result<bool>.Success(true); // idempotent

        // AC: 409 if department has active employees assigned (FR-26)
        var hasActiveEmployees = await _db.Users
            .AnyAsync(u => u.DepartmentId == id && u.IsActive, ct);

        if (hasActiveEmployees)
            return Result<bool>.Failure(
                "Cannot delete department: it has active employees assigned. " +
                "Reassign or deactivate employees first.", 409);

        dept.IsActive = false;
        dept.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _cache.Remove(ActiveCacheKey);

        return Result<bool>.Success(true);
    }

    private static DepartmentResponse MapToResponse(Department d) =>
        new(d.Id, d.Name, d.Description, d.IsActive, d.CreatedAt, d.UpdatedAt);
}
