using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace LMS.Tests.Integration.LeaveCore;

/// <summary>
/// Integration tests for leave request endpoints.
/// Covers IT-25 through IT-32 from docs/test-plan.md.
/// </summary>
[Trait("Category", "Integration")]
public class LeaveRequestIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public LeaveRequestIntegrationTests(CustomWebApplicationFactory factory)
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

    private async Task<Guid> GetFirstLeaveTypeIdAsync(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.GetAsync("/api/v1/leave-types");
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var arr  = json.GetProperty("data");
        return Guid.Parse(arr[0].GetProperty("id").GetString()!);
    }

    [Fact(DisplayName = "IT-25: POST /api/v1/leave-requests — Unpaid Leave with zero balance succeeds (UT-26)")]
    public async Task SubmitLeave_UnpaidLeave_ZeroBalance_Succeeds()
    {
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Find Unpaid Leave type
        var ltResp = await _client.GetAsync("/api/v1/leave-types");
        var ltJson = await ltResp.Content.ReadFromJsonAsync<JsonElement>();
        var unpaidId = ltJson.GetProperty("data").EnumerateArray()
            .First(x => x.GetProperty("name").GetString() == "Unpaid Leave")
            .GetProperty("id").GetString();

        var payload = new
        {
            leaveTypeId = Guid.Parse(unpaidId!),
            startDate   = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            endDate     = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            reason      = "Unpaid leave test"
        };

        var resp = await _client.PostAsJsonAsync("/api/v1/leave-requests", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "IT-26: POST /api/v1/leave-requests with overlapping dates returns 409")]
    public async Task SubmitLeave_OverlappingDates_Returns409()
    {
        var token = await GetAdminTokenAsync();
        var ltId  = await GetFirstLeaveTypeIdAsync(token);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60));
        var payload = new { leaveTypeId = ltId, startDate = futureDate, endDate = futureDate };

        // First submission
        await _client.PostAsJsonAsync("/api/v1/leave-requests", payload);

        // Second submission for same date → 409
        var resp = await _client.PostAsJsonAsync("/api/v1/leave-requests", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "IT-27: GET /api/v1/leave-requests returns own requests")]
    public async Task GetMyRequests_Returns200WithList()
    {
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/v1/leave-requests");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "IT-28: GET /api/v1/leave-requests/{id} with non-existent ID returns 404")]
    public async Task GetById_NonExistent_Returns404()
    {
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync($"/api/v1/leave-requests/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-29: PUT /api/v1/leave-requests/{id}/cancel with non-existent ID returns 404")]
    public async Task Cancel_NonExistent_Returns404()
    {
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.PutAsJsonAsync(
            $"/api/v1/leave-requests/{Guid.NewGuid()}/cancel", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-30: GET /api/v1/leave-requests/admin requires HRAdmin role")]
    public async Task AdminList_WithSuperAdmin_Returns200()
    {
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/v1/leave-requests/admin");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "IT-31: POST leave-request with invalid date range returns 422")]
    public async Task SubmitLeave_InvalidDateRange_Returns422()
    {
        var token = await GetAdminTokenAsync();
        var ltId  = await GetFirstLeaveTypeIdAsync(token);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            leaveTypeId = ltId,
            startDate   = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            endDate     = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)) // end < start
        };

        var resp = await _client.PostAsJsonAsync("/api/v1/leave-requests", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
