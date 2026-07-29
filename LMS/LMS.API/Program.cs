using System.Text;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using LMS.Application.Interfaces;
using LMS.Application.Settings;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Jobs;
using LMS.Infrastructure.Seeding;
using LMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// -- Serilog -------------------------------------------------------------------
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

// -- EF Core -- PostgreSQL ------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContext<LmsDbContext>(options =>
    options.UseNpgsql(connectionString));

// -- In-memory cache (used by ILeaveTypeService and callers) -------------------
builder.Services.AddMemoryCache();

// -- JWT Settings (bound from "JwtSettings" config section) --------------------
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();

// -- JWT Bearer Authentication --------------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim types as issued (no Microsoft claim mapping).
        // "role" claim stays as "role"; "sub" stays as ClaimTypes.NameIdentifier
        // via the default JwtSecurityTokenHandler mapping.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSettings.Issuer,
            ValidAudience            = jwtSettings.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            // Map the custom "role" claim so [Authorize(Roles=...)] works.
            RoleClaimType            = "role",
        };
    });

// -- Authorization policies ----------------------------------------------------
builder.Services.AddAuthorization(options =>
{
    // HRAdminOrAbove: requires the "role" JWT claim to be HRAdmin or SuperAdmin.
    options.AddPolicy("HRAdminOrAbove", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst("role")?.Value;
            return role is "HRAdmin" or "SuperAdmin";
        }));
});

// -- Application Services ------------------------------------------------------
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILeaveBalanceService, LeaveBalanceService>();

// -- Seed Service (idempotent startup seeder) ----------------------------------
builder.Services.AddHostedService<SeedService>();
builder.Services.AddScoped<ISeedService, SeedService>();

// -- Hangfire (PostgreSQL storage, schema = hangfire) --------------------------
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString),
        new PostgreSqlStorageOptions { SchemaName = "hangfire" }));
builder.Services.AddHangfireServer();

// -- Controllers ---------------------------------------------------------------
builder.Services.AddControllers();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// -- Hangfire Dashboard (HRAdmin role required via JWT claim) ------------------
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireJwtAuthorizationFilter() }
});

// -- Hangfire Recurring Jobs ---------------------------------------------------
// YearEndLapseJob  : 31 Dec 18:00 UTC (31 Dec 23:30 IST) — lapse old year first
// NewYearCreditJob : 31 Dec 18:30 UTC (01 Jan 00:00 IST) — then credit new year
// CompOffExpiryJob : daily 18:30 UTC (00:00 IST) — expire stale comp-off credits
RecurringJob.AddOrUpdate<YearEndLapseJob>(
    recurringJobId: "year-end-lapse",
    methodCall: j => j.Execute(),
    cronExpression: "0 18 31 12 *",
    timeZone: TimeZoneInfo.Utc);

RecurringJob.AddOrUpdate<NewYearCreditJob>(
    recurringJobId: "new-year-credit",
    methodCall: j => j.Execute(),
    cronExpression: "30 18 31 12 *",
    timeZone: TimeZoneInfo.Utc);

RecurringJob.AddOrUpdate<CompOffExpiryJob>(
    recurringJobId: "compoff-expiry",
    methodCall: j => j.Execute(),
    cronExpression: "30 18 * * *",
    timeZone: TimeZoneInfo.Utc);

app.MapControllers();

app.Run();

// ---------------------------------------------------------------------------
// Hangfire dashboard authorization: requires valid JWT with role=HRAdmin.
// Placed here (file-scoped) to avoid a separate class file for a small filter.
// ---------------------------------------------------------------------------
public class HangfireJwtAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        // User must be authenticated and carry the HRAdmin role claim
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole("HRAdmin");
    }
}
