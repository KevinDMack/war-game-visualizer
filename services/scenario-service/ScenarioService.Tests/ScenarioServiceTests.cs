using Microsoft.EntityFrameworkCore;
using WargameData;
using ScenarioService.Services;
using WargameVisualizer.Protos;
using Grpc.Core;

namespace ScenarioService.Tests;

/// <summary>
/// Unit tests for <see cref="ScenarioServiceImpl"/> using an in-memory
/// Entity Framework Core database.
/// Proto-generated <see cref="Scenario"/> objects are used directly as EF entities,
/// so there is no separate entity type to create in tests.
/// </summary>
public class ScenarioServiceTests : IDisposable
{
    private readonly WargameDbContext _db;
    private readonly ScenarioServiceImpl _service;

    public ScenarioServiceTests()
    {
        var options = new DbContextOptionsBuilder<WargameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WargameDbContext(options);
        _service = new ScenarioServiceImpl(_db);
    }

    public void Dispose() => _db.Dispose();

    // -----------------------------------------------------------------------
    // CreateScenario
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateScenario_WithValidRequest_ReturnsScenario()
    {
        var request = new CreateScenarioRequest
        {
            Scenario = new Scenario
            {
                ScenarioName = "Alpha Strike",
                Summary = "Test scenario",
                BoundingBox = new BoundingBox
                {
                    MinLatitude = 10.0, MinLongitude = 20.0,
                    MaxLatitude = 30.0, MaxLongitude = 40.0,
                },
            },
        };

        var response = await _service.CreateScenario(request, CreateContext());

        Assert.NotNull(response.Scenario);
        Assert.False(string.IsNullOrWhiteSpace(response.Scenario.ScenarioId));
        Assert.Equal("Alpha Strike", response.Scenario.ScenarioName);
        Assert.Equal("Test scenario", response.Scenario.Summary);
        Assert.Equal(10.0, response.Scenario.BoundingBox.MinLatitude);
    }

    [Fact]
    public async Task CreateScenario_WithExplicitId_PreservesId()
    {
        var id = Guid.NewGuid().ToString();
        var request = new CreateScenarioRequest
        {
            Scenario = new Scenario { ScenarioId = id, ScenarioName = "Bravo" },
        };

        var response = await _service.CreateScenario(request, CreateContext());

        Assert.Equal(id, response.Scenario.ScenarioId);
    }

    [Fact]
    public async Task CreateScenario_DuplicateId_ThrowsAlreadyExists()
    {
        var id = Guid.NewGuid().ToString();
        var request = new CreateScenarioRequest
        {
            Scenario = new Scenario { ScenarioId = id, ScenarioName = "Bravo" },
        };
        await _service.CreateScenario(request, CreateContext());

        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _service.CreateScenario(request, CreateContext()));

        Assert.Equal(StatusCode.AlreadyExists, ex.Status.StatusCode);
    }

    [Fact]
    public async Task CreateScenario_NullScenario_ThrowsInvalidArgument()
    {
        var request = new CreateScenarioRequest(); // Scenario is null

        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _service.CreateScenario(request, CreateContext()));

        Assert.Equal(StatusCode.InvalidArgument, ex.Status.StatusCode);
    }

    // -----------------------------------------------------------------------
    // GetScenario
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetScenario_ExistingId_ReturnsScenario()
    {
        var scenario = await CreateScenarioAsync("Charlie Strike");

        var response = await _service.GetScenario(
            new GetScenarioRequest { ScenarioId = scenario.ScenarioId }, CreateContext());

        Assert.Equal("Charlie Strike", response.Scenario.ScenarioName);
    }

    [Fact]
    public async Task GetScenario_NonExistentId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _service.GetScenario(
                new GetScenarioRequest { ScenarioId = Guid.NewGuid().ToString() },
                CreateContext()));

        Assert.Equal(StatusCode.NotFound, ex.Status.StatusCode);
    }

    // -----------------------------------------------------------------------
    // ListScenarios
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ListScenarios_ReturnsAllScenarios()
    {
        await CreateScenarioAsync("Delta 1");
        await CreateScenarioAsync("Delta 2");

        var response = await _service.ListScenarios(
            new ListScenariosRequest { PageSize = 10 }, CreateContext());

        Assert.Equal(2, response.Scenarios.Count);
    }

    [Fact]
    public async Task ListScenarios_Pagination_ReturnsNextPageToken()
    {
        for (int i = 0; i < 5; i++)
            await CreateScenarioAsync($"Echo {i}");

        var response = await _service.ListScenarios(
            new ListScenariosRequest { PageSize = 3 }, CreateContext());

        Assert.Equal(3, response.Scenarios.Count);
        Assert.False(string.IsNullOrEmpty(response.NextPageToken));
    }

    [Fact]
    public async Task ListScenarios_LastPage_HasNoNextPageToken()
    {
        for (int i = 0; i < 3; i++)
            await CreateScenarioAsync($"Foxtrot {i}");

        var response = await _service.ListScenarios(
            new ListScenariosRequest { PageSize = 10 }, CreateContext());

        Assert.True(string.IsNullOrEmpty(response.NextPageToken));
    }

    // -----------------------------------------------------------------------
    // UpdateScenario
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UpdateScenario_ExistingId_UpdatesFields()
    {
        var scenario = await CreateScenarioAsync("Golf Strike");

        var response = await _service.UpdateScenario(new UpdateScenarioRequest
        {
            Scenario = new Scenario
            {
                ScenarioId = scenario.ScenarioId,
                ScenarioName = "Golf Strike Updated",
                Summary = "Updated summary",
            },
        }, CreateContext());

        Assert.Equal("Golf Strike Updated", response.Scenario.ScenarioName);
        Assert.Equal("Updated summary", response.Scenario.Summary);
    }

    [Fact]
    public async Task UpdateScenario_NonExistentId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _service.UpdateScenario(new UpdateScenarioRequest
            {
                Scenario = new Scenario
                {
                    ScenarioId = Guid.NewGuid().ToString(),
                    ScenarioName = "Ghost",
                },
            }, CreateContext()));

        Assert.Equal(StatusCode.NotFound, ex.Status.StatusCode);
    }

    [Fact]
    public async Task UpdateScenario_NullScenario_ThrowsInvalidArgument()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _service.UpdateScenario(new UpdateScenarioRequest(), CreateContext()));

        Assert.Equal(StatusCode.InvalidArgument, ex.Status.StatusCode);
    }

    // -----------------------------------------------------------------------
    // DeleteScenario
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeleteScenario_ExistingId_ReturnsSuccess()
    {
        var scenario = await CreateScenarioAsync("Hotel Strike");

        var response = await _service.DeleteScenario(
            new DeleteScenarioRequest { ScenarioId = scenario.ScenarioId }, CreateContext());

        Assert.True(response.Success);
    }

    [Fact]
    public async Task DeleteScenario_ExistingId_RemovesFromDatabase()
    {
        var scenario = await CreateScenarioAsync("India Strike");

        await _service.DeleteScenario(
            new DeleteScenarioRequest { ScenarioId = scenario.ScenarioId }, CreateContext());

        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _service.GetScenario(
                new GetScenarioRequest { ScenarioId = scenario.ScenarioId }, CreateContext()));
        Assert.Equal(StatusCode.NotFound, ex.Status.StatusCode);
    }

    [Fact]
    public async Task DeleteScenario_NonExistentId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _service.DeleteScenario(
                new DeleteScenarioRequest { ScenarioId = Guid.NewGuid().ToString() },
                CreateContext()));

        Assert.Equal(StatusCode.NotFound, ex.Status.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ServerCallContext CreateContext() =>
        TestServerCallContext.Create();

    /// <summary>
    /// Creates and persists a <see cref="Scenario"/> proto object directly via EF Core.
    /// No separate entity type is needed since the proto class IS the EF entity.
    /// </summary>
    private async Task<Scenario> CreateScenarioAsync(string name)
    {
        var scenario = new Scenario
        {
            ScenarioId = Guid.NewGuid().ToString(),
            ScenarioName = name,
            Summary = $"Summary for {name}",
            BoundingBox = new BoundingBox
            {
                MinLatitude = 0, MinLongitude = 0,
                MaxLatitude = 1, MaxLongitude = 1,
            },
        };
        _db.Scenarios.Add(scenario);
        await _db.SaveChangesAsync();
        return scenario;
    }
}
