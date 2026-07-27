using System.Net;
using System.Net.Http.Json;
using LMS.Application.DTOs.Auth;
using LMS.Tests.Integration;
using Xunit;

namespace LMS.Tests.Integration.Auth;

/// <summary>
/// IT-6 — Middleware integration tests: JWT 401, CORS preflight, rate-limit 429.
/// Uses an in-process WebApplicationFactory (InMemory DB, no real PostgreSQL required).
/// </summary>
[Trait("Category", "Integration")]
public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// IT-6: A request to an [Authorize] endpoint with no JWT returns 401.
    /// JWT Bearer middleware rejects the request before the controller runs.
    /// </summary>
    [Fact(DisplayName = "IT-6: No JWT on protected endpoint returns 401")]
    public async Task NoJwt_ProtectedEndpoint_Returns401()
    {
        var client = _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // POST /logout requires [Authorize] — no JWT in headers
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new LogoutRequestDto { RefreshToken = "dummy-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// IT-6: A CORS preflight (OPTIONS) from the allowed frontend origin
    /// returns 204 with the Access-Control-Allow-Origin header set.
    /// </summary>
    [Fact(DisplayName = "IT-6: CORS preflight from allowed origin returns 204 with CORS headers")]
    public async Task CorsPreFlight_AllowedOrigin_Returns204WithCorsHeaders()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type,Authorization");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Response must contain Access-Control-Allow-Origin header");
        Assert.Equal(
            "http://localhost:5173",
            response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    /// <summary>
    /// IT-6: The sliding-window rate limiter allows 10 requests/minute per IP.
    /// The 11th request within the window must return 429 Too Many Requests.
    /// In test, all requests share partition key "loopback" (null RemoteIpAddress).
    /// </summary>
    [Fact(DisplayName = "IT-6: 11th login request in sliding window returns 429")]
    public async Task RateLimit_ElevenLoginRequests_Returns429OnEleventh()
    {
        // Use a separate client instance so rate-limit state does not leak from other tests
        var client = _factory.CreateClient();
        var payload = new LoginRequestDto { Email = "test@example.com", Password = "AnyPass1!" };

        HttpStatusCode lastStatus = HttpStatusCode.OK;
        for (int i = 1; i <= 11; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", payload);
            lastStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }
}
