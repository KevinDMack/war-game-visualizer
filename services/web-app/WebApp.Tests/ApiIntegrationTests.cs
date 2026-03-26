using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApp.Tests;

/// <summary>
/// Integration tests for the Web App API endpoints using an in-memory test server.
/// Dapr is not available in the test environment, so the in-memory fallback store is exercised.
/// Each test that mutates state creates its own isolated WebApplicationFactory instance.
/// </summary>
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // -----------------------------------------------------------------------
    // GET /healthz
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetHealthz_ReturnsOkWithStatusOk()
    {
        var response = await _client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\"", body);
        Assert.Contains("\"ok\"", body);
    }

    // -----------------------------------------------------------------------
    // GET /api/scenarios
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetScenarios_ReturnsOkWithScenariosArray()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/scenarios");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotNull(json?["scenarios"]);
    }

    // -----------------------------------------------------------------------
    // POST /api/scenarios
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PostScenario_ReturnsCreatedWithScenario()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/scenarios", new { name = "Alpha Strike" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Alpha Strike", json?["scenario"]?["name"]?.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(json?["scenario"]?["scenarioId"]?.GetValue<string>()));
    }

    [Fact]
    public async Task PostScenario_WithExplicitId_PreservesId()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        var id = Guid.NewGuid().ToString();

        var response = await client.PostAsJsonAsync("/api/scenarios", new { name = "Bravo Strike", scenarioId = id });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(id, json?["scenario"]?["scenarioId"]?.GetValue<string>());
    }

    // -----------------------------------------------------------------------
    // GET /api/scenarios/{id}
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetScenarioById_AfterPost_ReturnsScenario()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var postResp = await client.PostAsJsonAsync("/api/scenarios", new { name = "Charlie Strike" });
        var id = JsonNode.Parse(await postResp.Content.ReadAsStringAsync())?["scenario"]?["scenarioId"]?.GetValue<string>();
        Assert.NotNull(id);

        var getResp = await client.GetAsync($"/api/scenarios/{id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var json = JsonNode.Parse(await getResp.Content.ReadAsStringAsync());
        Assert.Equal("Charlie Strike", json?["scenario"]?["name"]?.GetValue<string>());
    }

    [Fact]
    public async Task GetScenarioById_NonExistentId_ReturnsNotFound()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/scenarios/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetScenarioById_InvalidId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/scenarios/not-a-valid-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // PUT /api/scenarios/{id}
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PutScenario_AfterPost_UpdatesScenario()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var postResp = await client.PostAsJsonAsync("/api/scenarios", new { name = "Delta Strike" });
        var id = JsonNode.Parse(await postResp.Content.ReadAsStringAsync())?["scenario"]?["scenarioId"]?.GetValue<string>();
        Assert.NotNull(id);

        var putResp = await client.PutAsJsonAsync($"/api/scenarios/{id}", new { name = "Delta Strike Updated" });
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);
        var json = JsonNode.Parse(await putResp.Content.ReadAsStringAsync());
        Assert.Equal("Delta Strike Updated", json?["scenario"]?["name"]?.GetValue<string>());
        Assert.Equal(id, json?["scenario"]?["scenarioId"]?.GetValue<string>());
    }

    [Fact]
    public async Task PutScenario_NonExistentId_ReturnsNotFound()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/scenarios/{Guid.NewGuid()}", new { name = "Ghost" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutScenario_InvalidId_ReturnsBadRequest()
    {
        var response = await _client.PutAsJsonAsync("/api/scenarios/not-a-guid", new { name = "Bad" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // DELETE /api/scenarios/{id}
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeleteScenario_AfterPost_ReturnsSuccess()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var postResp = await client.PostAsJsonAsync("/api/scenarios", new { name = "Echo Strike" });
        var id = JsonNode.Parse(await postResp.Content.ReadAsStringAsync())?["scenario"]?["scenarioId"]?.GetValue<string>();
        Assert.NotNull(id);

        var deleteResp = await client.DeleteAsync($"/api/scenarios/{id}");
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);
        var json = JsonNode.Parse(await deleteResp.Content.ReadAsStringAsync());
        Assert.True(json?["success"]?.GetValue<bool>());
    }

    [Fact]
    public async Task DeleteScenario_AfterPost_RemovesFromList()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var postResp = await client.PostAsJsonAsync("/api/scenarios", new { name = "Foxtrot Strike" });
        var id = JsonNode.Parse(await postResp.Content.ReadAsStringAsync())?["scenario"]?["scenarioId"]?.GetValue<string>();
        Assert.NotNull(id);

        await client.DeleteAsync($"/api/scenarios/{id}");
        var getResp = await client.GetAsync($"/api/scenarios/{id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task DeleteScenario_NonExistentId_ReturnsNotFound()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/scenarios/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteScenario_InvalidId_ReturnsBadRequest()
    {
        var response = await _client.DeleteAsync("/api/scenarios/not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // GET / (Razor page)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetRoot_ReturnsHtmlPage()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("text/html", contentType);
    }
}

