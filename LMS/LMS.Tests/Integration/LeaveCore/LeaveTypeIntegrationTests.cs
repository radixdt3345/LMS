using LMS.Application.DTOs.LeaveCore;
using LMS.Application.Interfaces;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using LMS.Infrastructure.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LMS.Tests.Integration.LeaveCore;

/// <summary>
/// Integration tests for IT-16: ILeaveTypeService CRUD + DI wiring.
/// Run: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class LeaveTypeIntegrationTests : IClassFixture<LeaveTypeIntegrationFactory>
{
    private readonly LeaveTypeIntegrationFactory _factory;

    public LeaveTypeIntegrationTests(LeaveTypeIntegrationFactory factory)
    {
        _factory = factory;
    }

    // ---- DI wiring verification (IT-16a, IT-16b) ----------------------------

    /// <summary>
    /// IT-16a: ILeaveTypeService must resolve to LeaveTypeService via DI.
    /// Verifies the Program.cs registration: AddScoped&lt;ILeaveTypeService, LeaveTypeService&gt;.
    /// </summary>
    [Fact]
    public void IT16a_ILeaveTypeService_ResolvesToLeaveTypeService()
    {
        using var scope   = _factory.Services.CreateScope();
        var service       = scope.ServiceProvider.GetService<ILeaveTypeService>();

        Assert.NotNull(service);
        Assert.IsType<LeaveTypeService>(service);
    }

    /// <summary>
    /// IT-16b: IMemoryCache must be registered in the DI container.
    /// Verifies AddMemoryCache() in Program.cs.
    /// </summary>
    [Fact]
    public void IT16b_IMemoryCache_IsRegistered()
    {
        using var scope = _factory.Services.CreateScope();
        var cache       = scope.ServiceProvider.GetService<IMemoryCache>();

        Assert.NotNull(cache);
    }

    // ---- CRUD integration tests (IT-16c – IT-16h) --------------------------
    // These tests use EF Core InMemory directly via the service constructor
    // to avoid HTTP overhead while still exercising the full service + EF layer.

    private static LmsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(opts);
    }

    /// <summary>
    /// IT-16c: CreateLeaveTypeAsync persists a leave type and returns a populated DTO.
    /// </summary>
    [Fact]
    public async Task IT16c_CreateLeaveTypeAsync_PersistsRecord()
    {
        await using var db = CreateDb();
        var svc = new LeaveTypeService(db);

        var dto    = new CreateLeaveTypeDto
        {
            Name           = "Annual Leave",
            MaxDaysPerYear = 21,
            AccrualType    = AccrualType.Annual,
            RequiresDocument = false,
        };
        var result = await svc.CreateLeaveTypeAsync(dto);

        Assert.True(result.IsSuccess);
        Assert.Equal("Annual Leave", result.Value.Name);
        Assert.Equal(21, result.Value.MaxDaysPerYear);
        Assert.True(result.Value.IsActive);

        // Verify it was persisted
        var count = await db.LeaveTypes.CountAsync();
        Assert.Equal(1, count);
    }

    /// <summary>
    /// IT-16d: GetLeaveTypesAsync returns only active types by default.
    /// </summary>
    [Fact]
    public async Task IT16d_GetLeaveTypesAsync_ReturnsOnlyActiveByDefault()
    {
        await using var db = CreateDb();
        var svc = new LeaveTypeService(db);

        await svc.CreateLeaveTypeAsync(new CreateLeaveTypeDto
        {
            Name = "Active Leave", AccrualType = AccrualType.Annual, MaxDaysPerYear = 12,
        });
        var created = await svc.CreateLeaveTypeAsync(new CreateLeaveTypeDto
        {
            Name = "Soon Inactive", AccrualType = AccrualType.OneTime, MaxDaysPerYear = 5,
        });
        // Deactivate the second one
        await svc.DeactivateLeaveTypeAsync(created.Value.Id);

        var result = await svc.GetLeaveTypesAsync();

        Assert.True(result.IsSuccess);
        var list = result.Value.ToList();
        Assert.Single(list);
        Assert.Equal("Active Leave", list[0].Name);
    }

    /// <summary>
    /// IT-16e: GetLeaveTypeByIdAsync returns the correct record.
    /// </summary>
    [Fact]
    public async Task IT16e_GetLeaveTypeByIdAsync_ReturnsCorrectRecord()
    {
        await using var db = CreateDb();
        var svc = new LeaveTypeService(db);

        var created = await svc.CreateLeaveTypeAsync(new CreateLeaveTypeDto
        {
            Name = "Sick Leave", AccrualType = AccrualType.Annual, MaxDaysPerYear = 10,
        });

        var result = await svc.GetLeaveTypeByIdAsync(created.Value.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Sick Leave", result.Value.Name);
        Assert.Equal(created.Value.Id, result.Value.Id);
    }

    /// <summary>
    /// IT-16e (not-found): GetLeaveTypeByIdAsync returns Failure for unknown ID.
    /// </summary>
    [Fact]
    public async Task IT16e_GetLeaveTypeByIdAsync_UnknownId_ReturnsFailure()
    {
        await using var db = CreateDb();
        var svc    = new LeaveTypeService(db);
        var result = await svc.GetLeaveTypeByIdAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
    }

    /// <summary>
    /// IT-16f: UpdateLeaveTypeAsync applies partial updates without touching unset fields.
    /// </summary>
    [Fact]
    public async Task IT16f_UpdateLeaveTypeAsync_AppliesPartialUpdate()
    {
        await using var db = CreateDb();
        var svc = new LeaveTypeService(db);

        var created = await svc.CreateLeaveTypeAsync(new CreateLeaveTypeDto
        {
            Name = "Casual Leave", AccrualType = AccrualType.Annual,
            MaxDaysPerYear = 8, RequiresDocument = false,
        });

        // Only update MaxDaysPerYear — Name should stay unchanged
        var updateDto = new UpdateLeaveTypeDto { MaxDaysPerYear = 12 };
        var result    = await svc.UpdateLeaveTypeAsync(created.Value.Id, updateDto);

        Assert.True(result.IsSuccess);
        Assert.Equal("Casual Leave", result.Value.Name);   // unchanged
        Assert.Equal(12, result.Value.MaxDaysPerYear);     // updated
    }

    /// <summary>
    /// IT-16g: DeactivateLeaveTypeAsync sets IsActive=false and is idempotent.
    /// </summary>
    [Fact]
    public async Task IT16g_DeactivateLeaveTypeAsync_SetsIsActiveFalse_Idempotent()
    {
        await using var db = CreateDb();
        var svc = new LeaveTypeService(db);

        var created = await svc.CreateLeaveTypeAsync(new CreateLeaveTypeDto
        {
            Name = "Maternity Leave", AccrualType = AccrualType.OneTime, MaxDaysPerYear = 90,
        });

        // First deactivation
        var r1 = await svc.DeactivateLeaveTypeAsync(created.Value.Id);
        Assert.True(r1.IsSuccess);

        // Idempotent second call — must also succeed
        var r2 = await svc.DeactivateLeaveTypeAsync(created.Value.Id);
        Assert.True(r2.IsSuccess);

        // Verify persisted state
        var lt = await db.LeaveTypes.FindAsync(created.Value.Id);
        Assert.NotNull(lt);
        Assert.False(lt!.IsActive);
    }

    /// <summary>
    /// IT-16h: GetLeaveTypesAsync with includeInactive=true returns all types.
    /// </summary>
    [Fact]
    public async Task IT16h_GetLeaveTypesAsync_IncludeInactive_ReturnsAll()
    {
        await using var db = CreateDb();
        var svc = new LeaveTypeService(db);

        var a = await svc.CreateLeaveTypeAsync(new CreateLeaveTypeDto
        {
            Name = "Active", AccrualType = AccrualType.Annual, MaxDaysPerYear = 12,
        });
        var b = await svc.CreateLeaveTypeAsync(new CreateLeaveTypeDto
        {
            Name = "Inactive", AccrualType = AccrualType.OneTime, MaxDaysPerYear = 5,
        });
        await svc.DeactivateLeaveTypeAsync(b.Value.Id);

        var result = await svc.GetLeaveTypesAsync(includeInactive: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count());
    }
}

/// <summary>
/// WebApplicationFactory for LeaveType integration tests.
/// Extends <see cref="CustomWebApplicationFactory"/> to also suppress the
/// Hangfire background server (which requires a real PostgreSQL connection).
/// </summary>
public class LeaveTypeIntegrationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Apply base factory: InMemory DB + remove SeedService
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Remove Hangfire background server hosted service(s).
            // AddHangfireServer() registers a hosted service that requires a live
            // PostgreSQL connection; in tests we swap it out for a no-op.
            var hangfireHosted = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                            && d.ImplementationType is not null
                            && d.ImplementationType.FullName != null
                            && d.ImplementationType.FullName.Contains("Hangfire"))
                .ToList();
            foreach (var d in hangfireHosted)
                services.Remove(d);
        });
    }
}
