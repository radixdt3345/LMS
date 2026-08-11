using LMS.Application.DTOs.Auth;
using LMS.Application.DTOs.People;
using LMS.Application.Interfaces;
using LMS.Application.Settings;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LMS.Tests.Integration.People;

/// <summary>
/// IT-11 to IT-15: EmployeeService integration tests against a real PostgreSQL test database.
///
/// IT-11: CreateEmployeeAsync persists a row and returns the new employee DTO.
/// IT-12: CreateEmployee with manager_id triggers DeriveRole — manager promoted to Manager.
/// IT-13: Assigning the same manager a second direct report is idempotent (stays Manager).
/// IT-14: DeactivateEmployeeAsync sets IsActive=false; AuthService.LoginAsync returns 401.
/// IT-15: GetTeamAsync returns only that manager's direct reports, not other managers'.
///
/// Requires PostgreSQL: set TEST_DB_CONNECTION env var, or defaults to
/// Host=localhost;Database=lms_test;Username=postgres;Password=postgres
///
/// Run: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class EmployeeIntegrationTests : IAsyncLifetime
{
    private LmsDbContext _db = null!;
    private AuditService _audit = null!;
    private EmployeeService _service = null!;

    // ── Shared JWT settings used in IT-14 ──────────────────────────────────
    private static readonly IOptions<JwtSettings> TestJwtOptions = Options.Create(new JwtSettings
    {
        SecretKey               = "test-secret-key-must-be-at-least-32-chars!!",
        Issuer                  = "lms-api",
        Audience                = "lms-client",
        AccessTokenExpiryMinutes = 15,
        RefreshTokenExpiryDays  = 7,
    });

    public async Task InitializeAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? "Host=localhost;Database=lms_test;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _db = new LmsDbContext(options);
        await _db.Database.MigrateAsync();

        _audit   = new AuditService(_db, NullLogger<AuditService>.Instance);
        _service = new EmployeeService(_db, _audit);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact(DisplayName = "IT-11: CreateEmployeeAsync persists user row to DB")]
    public async Task IT11_CreateEmployee_PersistsToDB()
    {
        var email = $"it11-{Guid.NewGuid():N}@test.com";
        var dto = new CreateEmployeeDto(
            Email:        email,
            Password:     "Password123!",
            AzureAdOid:   null,
            FirstName:    "Integration",
            LastName:     "Eleven",
            Phone:        null,
            EmployeeCode: $"E11-{Guid.NewGuid().ToString("N")[..6]}",
            DepartmentId: null,
            ManagerId:    null,
            Role:         null);

        var result = await _service.CreateEmployeeAsync(dto);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value!.Id);
        Assert.Equal(email, result.Value.Email);
        Assert.True(result.Value.IsActive);
        Assert.Equal("Employee", result.Value.Role);

        _db.ChangeTracker.Clear();
        var persisted = await _db.Users.FindAsync(result.Value.Id);
        Assert.NotNull(persisted);
        Assert.Equal(email, persisted.Email);
        Assert.True(persisted.IsActive);
        Assert.NotNull(persisted.PasswordHash);
    }

    [Fact(DisplayName = "IT-12: CreateEmployee with manager_id triggers DeriveRole → manager promoted")]
    public async Task IT12_CreateEmployee_WithManagerId_PromotesManager()
    {
        var mgrResult = await _service.CreateEmployeeAsync(new CreateEmployeeDto(
            Email: $"it12-mgr-{Guid.NewGuid():N}@test.com",
            Password: null, AzureAdOid: null,
            FirstName: "Manager", LastName: "Twelve",
            Phone: null, EmployeeCode: null,
            DepartmentId: null, ManagerId: null, Role: null));
        Assert.True(mgrResult.IsSuccess);
        var managerId = mgrResult.Value!.Id;

        _db.ChangeTracker.Clear();
        var before = await _db.Users.FindAsync(managerId);
        Assert.Equal(UserRole.Employee, before!.Role);

        var reportResult = await _service.CreateEmployeeAsync(new CreateEmployeeDto(
            Email: $"it12-report-{Guid.NewGuid():N}@test.com",
            Password: null, AzureAdOid: null,
            FirstName: "Report", LastName: "Twelve",
            Phone: null, EmployeeCode: null,
            DepartmentId: null, ManagerId: managerId, Role: null));
        Assert.True(reportResult.IsSuccess);

        _db.ChangeTracker.Clear();
        var after = await _db.Users.FindAsync(managerId);
        Assert.Equal(UserRole.Manager, after!.Role);
    }

    [Fact(DisplayName = "IT-13: Assigning same manager_id again leaves role as Manager (idempotent DeriveRole)")]
    public async Task IT13_DeriveRole_Idempotent_RemainsManager()
    {
        var mgrResult = await _service.CreateEmployeeAsync(new CreateEmployeeDto(
            Email: $"it13-mgr-{Guid.NewGuid():N}@test.com",
            Password: null, AzureAdOid: null,
            FirstName: "Idempotent", LastName: "Manager",
            Phone: null, EmployeeCode: null,
            DepartmentId: null, ManagerId: null, Role: null));
        var managerId = mgrResult.Value!.Id;

        await _service.CreateEmployeeAsync(new CreateEmployeeDto(
            Email: $"it13-r1-{Guid.NewGuid():N}@test.com",
            Password: null, AzureAdOid: null,
            FirstName: null, LastName: null, Phone: null, EmployeeCode: null,
            DepartmentId: null, ManagerId: managerId, Role: null));

        _db.ChangeTracker.Clear();
        var afterFirst = await _db.Users.FindAsync(managerId);
        Assert.Equal(UserRole.Manager, afterFirst!.Role);

        await _service.CreateEmployeeAsync(new CreateEmployeeDto(
            Email: $"it13-r2-{Guid.NewGuid():N}@test.com",
            Password: null, AzureAdOid: null,
            FirstName: null, LastName: null, Phone: null, EmployeeCode: null,
            DepartmentId: null, ManagerId: managerId, Role: null));

        _db.ChangeTracker.Clear();
        var afterSecond = await _db.Users.FindAsync(managerId);
        Assert.Equal(UserRole.Manager, afterSecond!.Role);
    }

    [Fact(DisplayName = "IT-14: DeactivateEmployee sets IsActive=false; AuthService returns 401 on login")]
    public async Task IT14_DeactivateEmployee_SubsequentLoginReturns401()
    {
        var email    = $"it14-{Guid.NewGuid():N}@test.com";
        const string password = "TestPass123!";

        var created = await _service.CreateEmployeeAsync(new CreateEmployeeDto(
            Email: email, Password: password,
            AzureAdOid: null, FirstName: null, LastName: null,
            Phone: null, EmployeeCode: null,
            DepartmentId: null, ManagerId: null, Role: null));
        Assert.True(created.IsSuccess);
        var employeeId = created.Value!.Id;

        var tokenService = new TokenService(TestJwtOptions, _db);
        var msalMock     = new Mock<IMsalAuthProvider>();
        var authService  = new AuthService(_db, tokenService, TestJwtOptions, msalMock.Object);

        var beforeDeactivate = await authService.LoginAsync(
            new LoginRequestDto { Email = email, Password = password });
        Assert.True(beforeDeactivate.IsSuccess);

        var deactivateResult = await _service.DeactivateEmployeeAsync(employeeId);
        Assert.True(deactivateResult.IsSuccess);

        _db.ChangeTracker.Clear();
        var deactivated = await _db.Users.FindAsync(employeeId);
        Assert.NotNull(deactivated);
        Assert.False(deactivated.IsActive, "IsActive must be false after deactivation");

        var loginResult = await authService.LoginAsync(
            new LoginRequestDto { Email = email, Password = password });
        Assert.False(loginResult.IsSuccess);
        Assert.Equal(401, loginResult.StatusCode);
    }

    [Fact(DisplayName = "IT-15: GetTeamAsync returns only direct reports of the specified manager")]
    public async Task IT15_GetTeamAsync_ReturnsOnlyDirectReports()
    {
        var mgr1 = await _service.CreateEmployeeAsync(new CreateEmployeeDto(
            Email: $"it15-mgr1-{Guid.NewGuid():N}@test.com",
            Password: null, AzureAdOid: null,
            FirstName: "Mgr1", LastName: "Fifteen",
            Phone: null, EmployeeCode: null,
            DepartmentId: null, ManagerId: null, Role: null));
        var mgr1Id = mgr1.Value!.Id;

        var mgr2 = await _service.CreateEmployeeAsync(new CreateEmployeeDto(
            Email: $"it15-mgr2-{Guid.NewGuid():N}@test.com",
            Password: null, AzureAdOid: null,
            FirstName: "Mgr2", LastName: "Fifteen",
            Phone: null, EmployeeCode: null,
            DepartmentId: null, ManagerId: null, Role: null));
        var mgr2Id = mgr2.Value!.Id;

        for (int i = 0; i < 2; i++)
        {
            await _service.CreateEmployeeAsync(new CreateEmployeeDto(
                Email: $"it15-mgr1-r{i}-{Guid.NewGuid():N}@test.com",
                Password: null, AzureAdOid: null,
                FirstName: $"Report{i}", LastName: "OfMgr1",
                Phone: null, EmployeeCode: null,
                DepartmentId: null, ManagerId: mgr1Id, Role: null));
        }

        await _service.CreateEmployeeAsync(new CreateEmployeeDto(
            Email: $"it15-mgr2-r0-{Guid.NewGuid():N}@test.com",
            Password: null, AzureAdOid: null,
            FirstName: "Report0", LastName: "OfMgr2",
            Phone: null, EmployeeCode: null,
            DepartmentId: null, ManagerId: mgr2Id, Role: null));

        var teamResult = await _service.GetTeamAsync(
            managerId: mgr1Id,
            callerId:  mgr1Id,
            callerIsHrAdmin: false);

        Assert.True(teamResult.IsSuccess);
        var team = teamResult.Value!.ToList();
        Assert.Equal(2, team.Count);
        Assert.All(team, emp => Assert.Equal(mgr1Id, emp.ManagerId));
        Assert.DoesNotContain(team, emp => emp.ManagerId == mgr2Id);
    }
}
