using LMS.Application.DTOs.LeaveCore;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Implements leave type management: CRUD with soft delete.
/// FR-27 (create), FR-28 (update), FR-29 (soft delete), FR-30 (no carry-forward).
/// </summary>
public class LeaveTypeService : ILeaveTypeService
{
    private readonly LmsDbContext _db;

    public LeaveTypeService(LmsDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<Result<IEnumerable<LeaveTypeDto>>> GetLeaveTypesAsync(
        bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _db.LeaveTypes.AsNoTracking();

        if (!includeInactive)
            query = query.Where(lt => lt.IsActive);

        var items = await query
            .OrderBy(lt => lt.Name)
            .Select(lt => new LeaveTypeDto
            {
                Id = lt.Id,
                Name = lt.Name,
                MaxDaysPerYear = lt.MaxDaysPerYear,
                AccrualType = lt.AccrualType,
                RequiresDocument = lt.RequiresDocument,
                IsActive = lt.IsActive,
            })
            .ToListAsync(ct);

        return Result<IEnumerable<LeaveTypeDto>>.Success(items);
    }

    /// <inheritdoc/>
    public async Task<Result<LeaveTypeDto>> GetLeaveTypeByIdAsync(
        Guid id, CancellationToken ct = default)
    {
        var lt = await _db.LeaveTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (lt is null)
            return Result<LeaveTypeDto>.Failure("Leave type not found.", 404);

        return Result<LeaveTypeDto>.Success(MapToDto(lt));
    }

    /// <inheritdoc/>
    public async Task<Result<LeaveTypeDto>> CreateLeaveTypeAsync(
        CreateLeaveTypeDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<LeaveTypeDto>.Failure("Name is required.", 400);

        var duplicate = await _db.LeaveTypes
            .AnyAsync(lt => lt.Name == dto.Name && lt.IsActive, ct);

        if (duplicate)
            return Result<LeaveTypeDto>.Failure(
                $"A leave type named '{dto.Name}' already exists.", 409);

        var now = DateTime.UtcNow;
        var entity = new LeaveType
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            MaxDaysPerYear = dto.MaxDaysPerYear,
            AccrualType = dto.AccrualType,
            RequiresDocument = dto.RequiresDocument,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.LeaveTypes.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Result<LeaveTypeDto>.Success(MapToDto(entity));
    }

    /// <inheritdoc/>
    public async Task<Result<LeaveTypeDto>> UpdateLeaveTypeAsync(
        Guid id, UpdateLeaveTypeDto dto, CancellationToken ct = default)
    {
        var entity = await _db.LeaveTypes.FindAsync(new object[] { id }, ct);

        if (entity is null)
            return Result<LeaveTypeDto>.Failure("Leave type not found.", 404);

        if (dto.Name is not null)
            entity.Name = dto.Name.Trim();

        if (dto.MaxDaysPerYear is not null)
            entity.MaxDaysPerYear = dto.MaxDaysPerYear;

        if (dto.AccrualType is not null)
            entity.AccrualType = dto.AccrualType.Value;

        if (dto.RequiresDocument is not null)
            entity.RequiresDocument = dto.RequiresDocument.Value;

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<LeaveTypeDto>.Success(MapToDto(entity));
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> DeactivateLeaveTypeAsync(
        Guid id, CancellationToken ct = default)
    {
        var entity = await _db.LeaveTypes.FindAsync(new object[] { id }, ct);

        if (entity is null)
            return Result<bool>.Failure("Leave type not found.", 404);

        if (!entity.IsActive)
            return Result<bool>.Success(true); // idempotent — already inactive

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    private static LeaveTypeDto MapToDto(LeaveType lt) => new()
    {
        Id = lt.Id,
        Name = lt.Name,
        MaxDaysPerYear = lt.MaxDaysPerYear,
        AccrualType = lt.AccrualType,
        RequiresDocument = lt.RequiresDocument,
        IsActive = lt.IsActive,
    };
}
