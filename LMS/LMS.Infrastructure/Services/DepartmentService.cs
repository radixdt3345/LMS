using LMS.Application.Common;
using LMS.Application.DTOs.People;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Department CRUD with IMemoryCache (cache-aside).
/// CONSTITUTION Rule 1: EF Core only, no raw SQL.
/// CONSTITUTION Rule 3: all timestamps stored as UTC.
/// </summary>
public class DepartmentService : IDepartmentService
{
    private readonly LmsDbContext _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    // Cache-version token key: removing this invalidates all list caches.
    private const string ListVersionKey = "departments:list_version";

    public DepartmentService(LmsDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResult<DepartmentDto>>> GetAllAsync(
        int page, int limit, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);

        var version = GetOrCreateListVersion();
        var cacheKey = $"departments:list:{version}:p={page}:l={limit}";

        if (_cache.TryGetValue(cacheKey, out PaginatedResult<DepartmentDto>? cached)
            && cached is not null)
            return Result<PaginatedResult<DepartmentDto>>.Success(cached);

        var query = _db.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(d => ToDto(d))
            .ToListAsync(ct);

        var result = new PaginatedResult<DepartmentDto>(items, total, page, limit);
        _cache.Set(cacheKey, result, CacheTtl);
        return Result<PaginatedResult<DepartmentDto>>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result<DepartmentDto>> GetByIdAsync(
        Guid id, CancellationToken ct = default)
    {
        var cacheKey = $"departments:id:{id}";
        if (_cache.TryGetValue(cacheKey, out DepartmentDto? cached)
            && cached is not null)
            return Result<DepartmentDto>.Success(cached);

        var dept = await _db.Departments.FindAsync([id], ct);
        if (dept is null || !dept.IsActive)
            return Result<DepartmentDto>.Failure("Department not found.", 404);

        var dto = ToDto(dept);
        _cache.Set(cacheKey, dto, CacheTtl);
        return Result<DepartmentDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<Result<DepartmentDto>> CreateAsync(
        CreateDepartmentDto dto, CancellationToken ct = default)
    {
        var nameConflict = await _db.Departments
            .AnyAsync(d => d.Name == dto.Name, ct);
        if (nameConflict)
            return Result<DepartmentDto>.Failure(
                "A department with this name already exists.", 409);

        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Departments.Add(dept);
        await _db.SaveChangesAsync(ct);
        InvalidateListCache();
        return Result<DepartmentDto>.Success(ToDto(dept));
    }

    /// <inheritdoc />
    public async Task<Result<DepartmentDto>> UpdateAsync(
        Guid id, UpdateDepartmentDto dto, CancellationToken ct = default)
    {
        var dept = await _db.Departments.FindAsync([id], ct);
        if (dept is null || !dept.IsActive)
            return Result<DepartmentDto>.Failure("Department not found.", 404);

        var nameConflict = await _db.Departments
            .AnyAsync(d => d.Name == dto.Name && d.Id != id, ct);
        if (nameConflict)
            return Result<DepartmentDto>.Failure(
                "A department with this name already exists.", 409);

        dept.Name = dto.Name;
        dept.Description = dto.Description;
        dept.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _cache.Remove($"departments:id:{id}");
        InvalidateListCache();
        return Result<DepartmentDto>.Success(ToDto(dept));
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteAsync(
        Guid id, CancellationToken ct = default)
    {
        var dept = await _db.Departments.FindAsync([id], ct);
        if (dept is null || !dept.IsActive)
            return Result<bool>.Failure("Department not found.", 404);

        dept.IsActive = false;
        dept.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _cache.Remove($"departments:id:{id}");
        InvalidateListCache();
        return Result<bool>.Success(true);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static DepartmentDto ToDto(Department d) =>
        new(d.Id, d.Name, d.Description, d.IsActive, d.CreatedAt, d.UpdatedAt);

    /// Returns (or creates) the current list-cache version token.
    private string GetOrCreateListVersion() =>
        _cache.GetOrCreate(
            ListVersionKey,
            e =>
            {
                e.Priority = CacheItemPriority.NeverRemove;
                return Guid.NewGuid().ToString("N");
            }) ?? Guid.NewGuid().ToString("N");

    /// Bust list caches by replacing the version token.
    /// Old version-qualified keys will simply never be hit again.
    private void InvalidateListCache() =>
        _cache.Set(ListVersionKey, Guid.NewGuid().ToString("N"),
            new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
}
