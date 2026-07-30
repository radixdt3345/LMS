using LMS.Application.DTOs.CompOff;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LMS.Tests.Unit.CompOff;

/// <summary>
/// Unit tests for CompOffRequestService and CompOffCreditService.
/// Covers UT-43 through UT-47 using the EF Core InMemory provider.
/// Run: dotnet test --filter Category=Unit
/// </summary>
[Trait("Category", "Unit")]
public class CompOffServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static LmsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(options);
    }

    private static User MakeUser() => new()
    {
        Id        = Guid.NewGuid(),
        Email     = $"u-{Guid.NewGuid()}@test.com",
        Role      = UserRole.Employee,
        IsActive  = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static CompOffRequest MakeRequest(
        Guid employeeId,
        decimal workedHours   = 8m,
        CompOffStatus status  = CompOffStatus.Approved) => new()
    {
        Id          = Guid.NewGuid(),
        EmployeeId  = employeeId,
        WorkedDate  = new DateOnly(2025, 1, 5),
        WorkedHours = workedHours,
        Status      = status,
        CreatedAt   = DateTime.UtcNow,
        UpdatedAt   = DateTime.UtcNow,
    };

    // ── UT-43: worked_hours < 4 → Failure (422) ────────────────────────────

    [Fact]
    public async Task UT43_Submit_HoursLessThan4_ReturnsFailure422()
    {
        await using var db = CreateDb();

        var holidaySvc = new Mock<IHolidayService>();
        var auditSvc   = new Mock<IAuditService>();
        var creditSvc  = new Mock<ICompOffCreditService>();

        var svc = new CompOffRequestService(
            db, holidaySvc.Object, auditSvc.Object, creditSvc.Object);

        var dto    = new CreateCompOffRequestDto { WorkedDate = new DateOnly(2025, 1, 5), WorkedHours = 3m };
        var result = await svc.SubmitAsync(Guid.NewGuid(), dto);

        Assert.False(result.IsSuccess);
        Assert.Equal(422, result.StatusCode);
        Assert.Contains("at least 4", result.Error, StringComparison.OrdinalIgnoreCase);

        // HolidayService must NOT be called when hours are already invalid
        holidaySvc.Verify(
            h => h.IsWorkingDayAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── UT-44: 4 hours → 0.5 day credit ───────────────────────────────────

    [Fact]
    public async Task UT44_CreditBalance_4Hours_Yields05Day()
    {
        await using var db = CreateDb();
        var user    = MakeUser();
        var request = MakeRequest(user.Id, workedHours: 4m);
        db.Users.Add(user);
        db.CompOffRequests.Add(request);
        await db.SaveChangesAsync();

        var svc    = new CompOffCreditService(db);
        var result = await svc.CreditBalanceAsync(request.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.5m, result.Value!.CreditDays);
    }

    // ── UT-45: 8 hours → 1.0 day credit ───────────────────────────────────

    [Fact]
    public async Task UT45_CreditBalance_8Hours_Yields10Day()
    {
        await using var db = CreateDb();
        var user    = MakeUser();
        var request = MakeRequest(user.Id, workedHours: 8m);
        db.Users.Add(user);
        db.CompOffRequests.Add(request);
        await db.SaveChangesAsync();

        var svc    = new CompOffCreditService(db);
        var result = await svc.CreditBalanceAsync(request.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1.0m, result.Value!.CreditDays);
    }

    // ── UT-46: expires_at = worked_date + 180 days ─────────────────────────

    [Fact]
    public async Task UT46_CreditBalance_ExpiresAt180DaysFromWorkedDate()
    {
        await using var db = CreateDb();
        var user       = MakeUser();
        var workedDate = new DateOnly(2025, 3, 10);
        var request    = new CompOffRequest
        {
            Id          = Guid.NewGuid(),
            EmployeeId  = user.Id,
            WorkedDate  = workedDate,
            WorkedHours = 8m,
            Status      = CompOffStatus.Approved,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.CompOffRequests.Add(request);
        await db.SaveChangesAsync();

        var svc    = new CompOffCreditService(db);
        var result = await svc.CreditBalanceAsync(request.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(workedDate.AddDays(180), result.Value!.ExpiresAt);
    }

    // ── UT-47: approved adds to comp-off leave balance ─────────────────────

    [Fact]
    public async Task UT47_CreditBalance_Approved_AddsToLeaveBalance()
    {
        await using var db = CreateDb();
        var user = MakeUser();

        // Seed the Comp Off leave type (name must contain "Comp", AccrualType.OneTime)
        var compOffType = new LeaveType
        {
            Id             = Guid.NewGuid(),
            Name           = "Comp Off",
            AccrualType    = AccrualType.OneTime,
            MaxDaysPerYear = null,
            IsActive       = true,
            CreatedAt      = DateTime.UtcNow,
            UpdatedAt      = DateTime.UtcNow,
        };
        db.LeaveTypes.Add(compOffType);

        var request = MakeRequest(user.Id, workedHours: 8m);
        db.Users.Add(user);
        db.CompOffRequests.Add(request);
        await db.SaveChangesAsync();

        var svc    = new CompOffCreditService(db);
        var result = await svc.CreditBalanceAsync(request.Id);

        Assert.True(result.IsSuccess);

        var balance = await db.LeaveBalances
            .FirstOrDefaultAsync(b =>
                b.UserId == user.Id &&
                b.LeaveTypeId == compOffType.Id);

        Assert.NotNull(balance);
        Assert.Equal(1.0m, balance.AllocatedDays);
        Assert.Equal(0m,   balance.UsedDays);
    }
}
