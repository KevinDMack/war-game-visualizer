// War Game Visualizer – Scenario Service
// ASP.NET Core 8 gRPC service that handles CRUD operations for wargame scenarios.
// Persists data to Azure SQL via Entity Framework Core (wargame.data library).

using Microsoft.EntityFrameworkCore;
using WargameData;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------

// Register the EF Core DbContext using the connection string from configuration.
// In Kubernetes the connection string is mounted from Azure Key Vault via the
// CSI secrets-store driver and exposed as an environment variable.
var connectionString =
    builder.Configuration.GetConnectionString("WargameDb")
    ?? Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "No database connection string found. " +
        "Set ConnectionStrings:WargameDb in appsettings.json or SQL_CONNECTION_STRING environment variable.");

builder.Services.AddDbContext<WargameDbContext>(options =>
    options.UseSqlServer(connectionString));

// Register the gRPC services
builder.Services.AddGrpc();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Apply pending EF Core migrations on startup
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
    db.Database.Migrate();
}

// ---------------------------------------------------------------------------
// Routes
// ---------------------------------------------------------------------------
app.MapGrpcService<ScenarioService.Services.ScenarioServiceImpl>();

// Health probe (used by Kubernetes liveness / readiness probes)
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();

// Make Program accessible for WebApplicationFactory in tests
public partial class Program { }
