using LMS.Application.DTOs.Auth;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Implements account management: listing user accounts with lockout state
/// and clearing lockout for a specific user. FR-10, FR-11.
/// </summary>
public class AccountService : IAccountService
{
    private readonly LmsDbContext _db;

    public AccountService(LmsDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<Result<PagedResult<AccountDto>>> GetAccountsAsync(
        int page, int limit, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var query = _db.Users.AsNoTracking();
        var total = await query.CountAsync(ct);

        var utcNow = DateTime.UtcNow;
        var items = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(u => new AccountDto
            {
                Id = u.Id,
                Email = u.Email,
                Role = u.Role.ToString(),
                IsActive = u.IsActive,
                IsLocked = u.LockoutUntil.HasValue && u.LockoutUntil > utcNow,
                LockoutUntil = u.LockoutUntil,
                FailedLoginCount = u.FailedLoginCount,
            })
            .ToListAsync(ct);

        return Result<PagedResult<AccountDto>>.Success(new PagedResult<AccountDto>
        {
            Items = items,
            Total = total,
            Page = page,
            Limit = limit,
        });
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> UnlockAccountAsync(
        Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, ct);

        if (user is null)
            return Result<bool>.Failure("User not found.", 404);

        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
