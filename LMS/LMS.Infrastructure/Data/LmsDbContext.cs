using LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Data;

/// <summary>
/// Main EF Core database context for the LMS application.
/// </summary>
public class LmsDbContext : DbContext
{
    public LmsDbContext(DbContextOptions<LmsDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<CompOffRequest> CompOffRequests => Set<CompOffRequest>();
    public DbSet<CompOffCredit> CompOffCredits => Set<CompOffCredit>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LmsDbContext).Assembly);
    }
}
