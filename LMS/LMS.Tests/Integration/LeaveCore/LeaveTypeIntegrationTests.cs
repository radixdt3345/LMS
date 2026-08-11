using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace LMS.Tests.Integration.LeaveCore;

[Trait("Category", "Integration")]
public class LeaveTypeIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public LeaveTypeIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@company.com", password = "Admin@1234" });
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("data").GetProperty("accessToken").GetString() ?? string.Empty;
    }

    [Fact(DisplayName = "IT-LT-1: GET /api/v1/leave-types returns seeded 7 types")]
    public async Task GetLeaveTypes_ReturnsSeededTypes()
    {
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/v1/leave-types");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("data").GetArrayLength().Should().Be(7);
    }

    [Fact(DisplayName = "IT-LT-2: POST /api/v1/leave-types creates a new leave type")]
    public async Task CreateLeaveType_Valid_Returns201()
    {
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            name               = "Test Leave " + Guid.NewGuid().ToString("N")[..8],
            isPaid             = true,
            defaultEntitlement = 5m,
            carryForward       = false
        };

        var resp = await _client.PostAsJsonAsync("/api/v1/leave-types", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "IT-LT-3: POST /api/v1/leave-types with duplicate name returns 409")]
    public async Task CreateLeaveType_DuplicateName_Returns409()
    {
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            name               = "Annual Leave", // already seeded
            isPaid             = true,
            defaultEntitlement = 18m,
            carryForward       = true
        };

        var resp = await _client.PostAsJsonAsync("/api/v1/leave-types", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
