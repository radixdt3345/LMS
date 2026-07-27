using LMS.Infrastructure.Data;
using LMS.Infrastructure.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LMS.Tests.Integration;

/// <summary>
/// In-process test host for integration tests.
/// Replaces PostgreSQL with InMemory DB and suppresses the SeedService hosted service
/// so tests can boot without a real database connection.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"]   = "unused-in-test",
                ["JwtSettings:SecretKey"]                = "test-secret-key-must-be-at-least-32-chars!!",
                ["JwtSettings:Issuer"]                   = "lms-api",
                ["JwtSettings:Audience"]                 = "lms-client",
                ["JwtSettings:AccessTokenExpiryMinutes"] = "15",
                ["AzureAd:TenantId"]                     = "test-tenant-id",
                ["AzureAd:ClientId"]                     = "test-client-id",
                ["FRONTEND_ORIGIN"]                      = "http://localhost:5173",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace PostgreSQL DbContext with InMemory so no DB is needed at test boot
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<LmsDbContext>));
            if (dbDescriptor != null)
                services.Remove(dbDescriptor);

            services.AddDbContext<LmsDbContext>(options =>
                options.UseInMemoryDatabase("LmsIntegrationTestDb_" + Guid.NewGuid()));

            // Remove SeedService HostedService — it requires a real PostgreSQL connection
            var seedHosted = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                            && d.ImplementationType == typeof(SeedService))
                .ToList();
            foreach (var descriptor in seedHosted)
                services.Remove(descriptor);
        });
    }
}
