// War Game Visualizer – Web Application
// ASP.NET Core 8 application that serves the Cesium-based globe UI and proxies
// scenario CRUD operations to the scenario-service via Dapr.

using System.Text.Json;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------
if (!int.TryParse(Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3500", out int daprHttpPort))
{
    daprHttpPort = 3500;
}
string scenarioServiceAppId = Environment.GetEnvironmentVariable("SCENARIO_SERVICE_APP_ID") ?? "scenario-service";
string daprBaseUrl = $"http://localhost:{daprHttpPort}/v1.0/invoke/{scenarioServiceAppId}/method";

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------
builder.Services.AddRazorPages();

builder.Services.AddHttpClient("dapr", client =>
{
    client.BaseAddress = new Uri($"http://localhost:{daprHttpPort}");
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// In-memory fallback store (used when running locally without the scenario-service)
// ---------------------------------------------------------------------------
var scenarios = new List<JsonObject>();

// ---------------------------------------------------------------------------
// Helper: check if Dapr sidecar is reachable
// ---------------------------------------------------------------------------
async Task<bool> IsDaprAvailable(IHttpClientFactory factory)
{
    try
    {
        using var checkClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var response = await checkClient.GetAsync($"http://localhost:{daprHttpPort}/v1.0/healthz");
        return response.StatusCode == System.Net.HttpStatusCode.NoContent;
    }
    catch
    {
        return false;
    }
}

// ---------------------------------------------------------------------------
// Middleware
// ---------------------------------------------------------------------------
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

// ---------------------------------------------------------------------------
// Routes – REST API (consumed by the front-end via fetch)
// ---------------------------------------------------------------------------

// GET /api/scenarios
app.MapGet("/api/scenarios", async (IHttpClientFactory factory) =>
{
    if (await IsDaprAvailable(factory))
    {
        var client = factory.CreateClient("dapr");
        var resp = await client.GetAsync($"{daprBaseUrl}/scenarios");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        return Results.Content(json, "application/json");
    }
    return Results.Ok(new { scenarios });
});

// GET /api/scenarios/{id}
app.MapGet("/api/scenarios/{id}", async (string id, IHttpClientFactory factory) =>
{
    if (!Guid.TryParse(id, out _))
        return Results.BadRequest(new { error = "Invalid scenario ID format" });

    if (await IsDaprAvailable(factory))
    {
        var client = factory.CreateClient("dapr");
        var resp = await client.GetAsync($"{daprBaseUrl}/scenarios/{id}");
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return Results.NotFound(new { error = "Not found" });
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        return Results.Content(json, "application/json");
    }
    var scenario = scenarios.FirstOrDefault(s =>
        s.TryGetPropertyValue("scenarioId", out var v) && v?.GetValue<string>() == id);
    return scenario is not null ? Results.Ok(new { scenario }) : Results.NotFound(new { error = "Not found" });
});

// POST /api/scenarios
app.MapPost("/api/scenarios", async (HttpRequest request, IHttpClientFactory factory) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    var data = JsonNode.Parse(body)?.AsObject() ?? new JsonObject();
    if (!data.ContainsKey("scenarioId"))
        data["scenarioId"] = Guid.NewGuid().ToString();

    if (await IsDaprAvailable(factory))
    {
        var client = factory.CreateClient("dapr");
        var sanitized = new StringContent(data.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PostAsync($"{daprBaseUrl}/scenarios", sanitized);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        return Results.Content(json, "application/json", statusCode: 201);
    }

    scenarios.Add(data);
    return Results.Created($"/api/scenarios/{data["scenarioId"]}", new { scenario = data });
});

// PUT /api/scenarios/{id}
app.MapPut("/api/scenarios/{id}", async (string id, HttpRequest request, IHttpClientFactory factory) =>
{
    if (!Guid.TryParse(id, out _))
        return Results.BadRequest(new { error = "Invalid scenario ID format" });

    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    var data = JsonNode.Parse(body)?.AsObject() ?? new JsonObject();
    data["scenarioId"] = id;

    if (await IsDaprAvailable(factory))
    {
        var client = factory.CreateClient("dapr");
        var sanitized = new StringContent(data.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PutAsync($"{daprBaseUrl}/scenarios/{id}", sanitized);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return Results.NotFound(new { error = "Not found" });
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        return Results.Content(json, "application/json");
    }
    var idx = scenarios.FindIndex(s =>
        s.TryGetPropertyValue("scenarioId", out var v) && v?.GetValue<string>() == id);
    if (idx < 0)
        return Results.NotFound(new { error = "Not found" });
    scenarios[idx] = data;
    return Results.Ok(new { scenario = data });
});

// DELETE /api/scenarios/{id}
app.MapDelete("/api/scenarios/{id}", async (string id, IHttpClientFactory factory) =>
{
    if (!Guid.TryParse(id, out _))
        return Results.BadRequest(new { error = "Invalid scenario ID format" });

    if (await IsDaprAvailable(factory))
    {
        var client = factory.CreateClient("dapr");
        var resp = await client.DeleteAsync($"{daprBaseUrl}/scenarios/{id}");
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return Results.NotFound(new { error = "Not found" });
        resp.EnsureSuccessStatusCode();
        return Results.Ok(new { success = true });
    }
    var before = scenarios.Count;
    scenarios.RemoveAll(s => s.TryGetPropertyValue("scenarioId", out var v) && v?.GetValue<string>() == id);
    return scenarios.Count < before
        ? Results.Ok(new { success = true })
        : Results.NotFound(new { error = "Not found" });
});

// ---------------------------------------------------------------------------
// Health probe (used by Kubernetes liveness / readiness probes)
// ---------------------------------------------------------------------------
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();

// Make Program accessible for WebApplicationFactory in tests
public partial class Program { }
