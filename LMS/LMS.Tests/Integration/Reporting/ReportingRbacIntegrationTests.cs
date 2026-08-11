using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace LMS.Tests.Integration.Reporting;

[Trait("Category", "Integration")]
public class ReportingRbacIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string TestSecretKey = "test-secret-key-must-be-at-least-32-chars!!";
    private const string TestIssuer    = "lms-api";
    private const string TestAudience  = "lms-client";

    private readonly CustomWebApplicationFactory _factory;

    public ReportingRbacIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

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

    [Fact]
    public async Task IT52_HrDashboard_EmployeeRole_Returns403Forbidden()
    {
        var client  = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard/hr");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", MintJwt("Employee"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task IT53_EmployeeDashboard_NoAuthHeader_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dashboard/employee");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
