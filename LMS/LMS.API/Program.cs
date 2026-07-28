using LMS.Application.Interfaces;
using LMS.Application.Settings;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Seeding;
using LMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .Build())
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

// EF Core — PostgreSQL
builder.Services.AddDbContext<LmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<AzureAdSettings>(builder.Configuration.GetSection("AzureAd"));

var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();

// MSAL config validation — warn on startup if placeholders remain (FR-5)
var azureTenantId = builder.Configuration["AzureAd:TenantId"] ?? string.Empty;
var azureClientId = builder.Configuration["AzureAd:ClientId"] ?? string.Empty;
if (string.IsNullOrWhiteSpace(azureTenantId)
    || azureTenantId.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
    || string.IsNullOrWhiteSpace(azureClientId)
    || azureClientId.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
{
    Log.Warning(
        "[AUTH] AzureAd configuration is incomplete or contains placeholder values. "
        + "Set AzureAd:TenantId and AzureAd:ClientId via environment variables. "
        + "SSO login (FR-5, FR-6) will not function until these are set.");
}

// Rate limiting — 10 req/min sliding window per IP on POST /api/v1/auth/login (FR-10)
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "loopback",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }
        )
    );
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// CORS — allow configured frontend origin (default: Vite dev server)
var frontendOrigin = builder.Configuration["FRONTEND_ORIGIN"] ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(frontendOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Authentication — JWT Bearer
// RoleClaimType = "role" maps to the "role" claim emitted by TokenService
// so [Authorize(Roles="HrAdmin")] etc. resolves correctly.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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
            RoleClaimType            = "role"  // TokenService emits "role", not ClaimTypes.Role
        };
    });

// Application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IMsalAuthProvider, MsalAuthProvider>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ILeaveTypeService, LeaveTypeService>();
builder.Services.AddScoped<IHolidayService, HolidayService>();

// Seeding
builder.Services.AddHostedService<SeedService>();
builder.Services.AddScoped<ISeedService, SeedService>();

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

// Middleware pipeline order matters:
// UseRouting → UseCors (before auth) → UseRateLimiter → UseAuthentication → UseAuthorization → endpoints
app.UseRouting();
app.UseCors("FrontendPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Expose Program to test assembly via WebApplicationFactory<Program>
public partial class Program { }
