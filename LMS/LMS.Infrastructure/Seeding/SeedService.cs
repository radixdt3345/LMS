using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BC = BCrypt.Net.BCrypt;

namespace LMS.Infrastructure.Seeding;

/// <summary>
/// Runs at startup to seed essential data idempotently.
/// App starts normally even if seeding fails (errors are logged, not re-thrown).
/// </summary>
public class SeedService : IHostedService, ISeedService
{
    private readonly LmsDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<SeedService> _logger;

    public SeedService(LmsDbContext db, IConfiguration config, ILogger<SeedService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await SeedAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seeding failed — application will continue without seed data.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Runs all seed operations. Safe to call multiple times — no duplicates created.
    /// </summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Guard: in-memory provider does not support MigrateAsync
        if (_db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            await _db.Database.MigrateAsync(ct);

        await SeedDepartmentsAsync(ct);
        await SeedUsersAsync(ct);
        await SeedLeaveTypesAsync(ct);
    }

    private async Task SeedDepartmentsAsync(CancellationToken ct)
    {
        if (!await _db.Departments.AnyAsync(d => d.Name == "HR", ct))
        {
            _db.Departments.Add(new Department
            {
                Id = Guid.NewGuid(),
                Name = "HR",
                Description = "Human Resources department",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded HR department.");
        }
    }

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        var superAdminEmail = _config["Seed:SuperAdminEmail"] ?? "superadmin@lms.local";
        var superAdminPassword = _config["Seed:SuperAdminPassword"] ?? "SuperAdmin@123";
        var hrAdminEmail = _config["Seed:HrAdminEmail"] ?? "hradmin@lms.local";
        var hrAdminPassword = _config["Seed:HrAdminPassword"] ?? "HrAdmin@123";

        if (!await _db.Users.AnyAsync(u => u.Email == superAdminEmail, ct))
        {
            _db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = superAdminEmail,
                PasswordHash = BC.HashPassword(superAdminPassword),
                Role = UserRole.SuperAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _logger.LogInformation("Seeded SuperAdmin user.");
        }

        if (!await _db.Users.AnyAsync(u => u.Email == hrAdminEmail, ct))
        {
            _db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = hrAdminEmail,
                PasswordHash = BC.HashPassword(hrAdminPassword),
                Role = UserRole.HRAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _logger.LogInformation("Seeded HRAdmin user.");
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedLeaveTypesAsync(CancellationToken ct)
    {
        var leaveTypes = new[]
        {
            new LeaveType { Id = Guid.NewGuid(), Name = "Annual Leave",              MaxDaysPerYear = 18,   AccrualType = AccrualType.Annual,    IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveType { Id = Guid.NewGuid(), Name = "Sick Leave",                MaxDaysPerYear = 10,   AccrualType = AccrualType.Annual,    IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveType { Id = Guid.NewGuid(), Name = "Casual Leave",              MaxDaysPerYear = 7,    AccrualType = AccrualType.Annual,    IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveType { Id = Guid.NewGuid(), Name = "Unpaid Leave",              MaxDaysPerYear = null, AccrualType = AccrualType.Unlimited, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveType { Id = Guid.NewGuid(), Name = "Maternity/Paternity Leave", MaxDaysPerYear = 90,   AccrualType = AccrualType.OneTime,   IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        };

        foreach (var lt in leaveTypes)
        {
            if (!await _db.LeaveTypes.AnyAsync(x => x.Name == lt.Name, ct))
            {
                _db.LeaveTypes.Add(lt);
                _logger.LogInformation("Seeded leave type: {Name}", lt.Name);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
