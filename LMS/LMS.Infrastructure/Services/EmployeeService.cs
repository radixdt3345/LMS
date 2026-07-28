using LMS.Application.DTOs.People;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Employee management service. FR-12 to FR-20.
/// Domain boundary: reads/writes only the users table plus a read-only join
/// against departments for DepartmentName projection.
/// AuditService.LogAsync called on every mutation (CONSTITUTION Art II).
/// </summary>
public class EmployeeService : IEmployeeService
{
    private readonly LmsDbContext _db;
    private readonly IAuditService _audit;

    public EmployeeService(LmsDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<EmployeeDto>>> GetEmployeesAsync(
        int page, int limit, Guid? deptId, string? search, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (limit < 1 || limit > 100) limit = 20;

        var query = _db.Users.AsNoTracking().Where(u => u.IsActive);

        if (deptId.HasValue)
            query = query.Where(u => u.DepartmentId == deptId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(term) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(term)) ||
                (u.LastName  != null && u.LastName.ToLower().Contains(term))  ||
                (u.EmployeeCode != null && u.EmployeeCode.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(ct);

        // Batch-fetch department names for the current page only
        var deptIds = users
            .Where(u => u.DepartmentId.HasValue)
            .Select(u => u.DepartmentId!.Value)
            .Distinct()
            .ToList();

        var deptMap = deptIds.Count > 0
            ? await _db.Departments.AsNoTracking()
                .Where(d => deptIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name, ct)
            : new Dictionary<Guid, string>();

        var items = users.Select(u => MapToDto(u, u.DepartmentId.HasValue
            ? deptMap.GetValueOrDefault(u.DepartmentId.Value)
            : null)).ToList();

        return Result<PagedResult<EmployeeDto>>.Success(new PagedResult<EmployeeDto>
        {
            Items  = items,
            Total  = total,
            Page   = page,
            Limit  = limit
        });
    }

    /// <inheritdoc />
    public async Task<Result<EmployeeDto>> GetEmployeeByIdAsync(
        Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
            return Result<EmployeeDto>.Failure("Employee not found.", 404);

        string? deptName = null;
        if (user.DepartmentId.HasValue)
        {
            deptName = await _db.Departments.AsNoTracking()
                .Where(d => d.Id == user.DepartmentId.Value)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(ct);
        }

        return Result<EmployeeDto>.Success(MapToDto(user, deptName));
    }

    /// <inheritdoc />
    public async Task<Result<EmployeeDto>> CreateEmployeeAsync(
        CreateEmployeeDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return Result<EmployeeDto>.Failure("Email is required.", 400);

        var exists = await _db.Users.AnyAsync(u => u.Email == dto.Email.Trim(), ct);
        if (exists)
            return Result<EmployeeDto>.Failure(
                $"An employee with email '{dto.Email}' already exists.", 409);

        // Parse role; default to Employee when omitted or unrecognised
        var role = UserRole.Employee;
        if (!string.IsNullOrWhiteSpace(dto.Role))
            Enum.TryParse<UserRole>(dto.Role, ignoreCase: true, out role);

        string? passwordHash = null;
        if (!string.IsNullOrWhiteSpace(dto.Password))
            passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Email        = dto.Email.Trim(),
            PasswordHash = passwordHash,
            AzureAdOid   = dto.AzureAdOid?.Trim(),
            DepartmentId = dto.DepartmentId,
            ManagerId    = dto.ManagerId,
            Role         = role,
            IsActive     = true,
            CreatedAt    = now,
            UpdatedAt    = now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("User", user.Id.ToString(), "Create", null,
            $"Created employee {user.Email}", ct);

        return Result<EmployeeDto>.Success(MapToDto(user, null));
    }

    /// <inheritdoc />
    public async Task<Result<EmployeeDto>> UpdateEmployeeAsync(
        Guid id, UpdateEmployeeDto dto, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
            return Result<EmployeeDto>.Failure("Employee not found.", 404);

        if (dto.FirstName    is not null) user.FirstName    = dto.FirstName.Trim();
        if (dto.LastName     is not null) user.LastName     = dto.LastName.Trim();
        if (dto.Phone        is not null) user.Phone        = dto.Phone.Trim();
        if (dto.EmployeeCode is not null) user.EmployeeCode = dto.EmployeeCode.Trim();
        if (dto.DepartmentId is not null) user.DepartmentId = dto.DepartmentId;
        if (dto.ManagerId    is not null) user.ManagerId    = dto.ManagerId;
        if (dto.IsActive     is not null) user.IsActive     = dto.IsActive.Value;
        if (!string.IsNullOrWhiteSpace(dto.Role)
            && Enum.TryParse<UserRole>(dto.Role, ignoreCase: true, out var parsedRole))
            user.Role = parsedRole;

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("User", user.Id.ToString(), "Update", null,
            $"Updated employee {user.Email}", ct);

        // Fetch dept name for updated record
        string? deptName = null;
        if (user.DepartmentId.HasValue)
        {
            deptName = await _db.Departments.AsNoTracking()
                .Where(d => d.Id == user.DepartmentId.Value)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(ct);
        }

        return Result<EmployeeDto>.Success(MapToDto(user, deptName));
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeactivateEmployeeAsync(
        Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);

        if (user is null)
            return Result<bool>.Failure("Employee not found.", 404);

        if (!user.IsActive)
            return Result<bool>.Success(true); // idempotent

        user.IsActive  = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("User", user.Id.ToString(), "Deactivate", null,
            $"Deactivated employee {user.Email}", ct);

        return Result<bool>.Success(true);
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private static EmployeeDto MapToDto(User user, string? deptName) =>
        new(
            user.Id,
            user.Email,
            user.Role.ToString(),
            user.DepartmentId,
            deptName,
            user.ManagerId,
            user.IsActive,
            user.CreatedAt
        );
}
