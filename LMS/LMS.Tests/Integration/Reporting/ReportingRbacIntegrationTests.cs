using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace LMS.Tests.Integration.Reporting;

/// <summary>
/// Integration tests IT-52 and IT-53 for REPORTING-INT-001 (RBAC on dashboard endpoints).
///
/// IT-52: An Employee-role JWT calling GET /api/v1/dashboard/hr must receive 403 Forbidden.
///        The endpoint carries [Authorize(Roles = "HRAdmin,SuperAdmin")] — an authenticated
///        user outside those roles must be forbidden, not challenged with 401.
///
/// IT-53: An unauthenticated request (no Authorization header) to GET /api/v1/dashboard/employee
///        must receive 401 Unauthorized. The controller class carries [Authorize], so the JWT
///        middleware rejects missing tokens before the action is reached.
///
/// Both tests spin up the full ASP.NET Core pipeline in-process via CustomWebApplicationFactory
/// (InMemory DB, SeedService suppressed, real JWT middleware active). The JWT is minted locally
/// using the same secret/issuer/audience the factory injects via AddInMemoryCollection, so the
/// middleware accepts it as a genuine token without any external auth call.
/// </summary>
[Trait("Category", "Integration")]
public class ReportingRbacIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    // Must exactly match the values CustomWebApplicationFactory injects into IConfiguration.
    private const string TestSecretKey = "test-secret-key-must-be-at-least-32-chars!!";
    private const string TestIssuer    = "lms-api";
    private const string TestAudience  = "lms-client";

    private readonly CustomWebApplicationFactory _factory;

    public ReportingRbacIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Mints a minimal, validly-signed JWT for the given role.
    /// The role claim is keyed <c>"role"</c> to match Program.cs:
    ///   options.TokenValidationParameters.RoleClaimType = "role";
    /// ASP.NET Core's [Authorize(Roles = "...")] then resolves roles via that claim.
    /// </summary>
    private static string MintJwt(string role)
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub",  Guid.NewGuid().ToString()),
            new Claim("role", role),
        };

        var token = new JwtSecurityToken(
            issuer:             TestIssuer,
            audience:           TestAudience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ── IT-52 ────────────────────────────────────────────────────────────────
    // RBAC: Employee-role JWT on GET /api/v1/dashboard/hr → 403 Forbidden

    /// <summary>
    /// IT-52: A request to GET /api/v1/dashboard/hr carrying a valid JWT whose
    /// role claim is "Employee" must be answered with 403 Forbidden.
    ///
    /// The token is accepted by the JWT middleware (valid signature, issuer, audience,
    /// lifetime) so the request is authenticated. However [Authorize(Roles = "HRAdmin,SuperAdmin")]
    /// on the action rejects the Employee role at the authorisation step, producing 403 — not 401.
    /// </summary>
    [Fact]
    public async Task IT52_HrDashboard_EmployeeRole_Returns403Forbidden()
    {
        // Arrange
        var client  = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard/hr");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", MintJwt("Employee"));

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── IT-53 ────────────────────────────────────────────────────────────────
    // ProtectedRoute: no Authorization header on GET /api/v1/dashboard/employee → 401 Unauthorized

    /// <summary>
    /// IT-53: A request to GET /api/v1/dashboard/employee with NO Authorization header
    /// must be answered with 401 Unauthorized.
    ///
    /// DashboardController carries class-level [Authorize], so the JWT middleware
    /// challenges the request (returning 401) before the action is ever invoked.
    /// This verifies the backend auth guard — the server-side equivalent of the
    /// React ProtectedRoute that would redirect unauthenticated users on the client.
    /// </summary>
    [Fact]
    public async Task IT53_EmployeeDashboard_NoAuthHeader_Returns401Unauthorized()
    {
        // Arrange — fresh client created directly from the factory, no auth header set
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/dashboard/employee");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
