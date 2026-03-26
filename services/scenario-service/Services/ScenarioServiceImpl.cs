using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using WargameData;
using WargameData.Entities;
using WargameVisualizer.Protos;

namespace ScenarioService.Services;

/// <summary>
/// gRPC service implementation for the <see cref="WargameVisualizer.Protos.ScenarioService"/>
/// defined in scenario.proto.  Handles all CRUD operations for wargame scenarios
/// using Entity Framework Core via <see cref="WargameDbContext"/>.
/// </summary>
public class ScenarioServiceImpl : WargameVisualizer.Protos.ScenarioService.ScenarioServiceBase
{
    private readonly WargameDbContext _db;

    /// <summary>
    /// Initializes a new instance of <see cref="ScenarioServiceImpl"/> with the
    /// injected <see cref="WargameDbContext"/>.
    /// </summary>
    public ScenarioServiceImpl(WargameDbContext db)
    {
        _db = db;
    }

    // -----------------------------------------------------------------------
    // GetScenario
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public override async Task<GetScenarioResponse> GetScenario(
        GetScenarioRequest request, ServerCallContext context)
    {
        var entity = await _db.Scenarios
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ScenarioId == request.ScenarioId,
                                 context.CancellationToken);

        if (entity is null)
        {
            throw new RpcException(
                new Status(StatusCode.NotFound,
                           $"Scenario '{request.ScenarioId}' not found."));
        }

        return new GetScenarioResponse { Scenario = ToProto(entity) };
    }

    // -----------------------------------------------------------------------
    // ListScenarios
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public override async Task<ListScenariosResponse> ListScenarios(
        ListScenariosRequest request, ServerCallContext context)
    {
        int pageSize = request.PageSize > 0 ? request.PageSize : 20;

        // page_token encodes the zero-based page index as a plain integer string
        int pageIndex = 0;
        if (!string.IsNullOrEmpty(request.PageToken) &&
            int.TryParse(request.PageToken, out int parsedIndex) &&
            parsedIndex > 0)
        {
            pageIndex = parsedIndex;
        }

        var query = _db.Scenarios.AsNoTracking().OrderBy(s => s.ScenarioId);
        int totalCount = await query.CountAsync(context.CancellationToken);

        var entities = await query
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(context.CancellationToken);

        var response = new ListScenariosResponse();
        response.Scenarios.AddRange(entities.Select(ToProto));

        // Provide next page token when there are more results
        bool hasNextPage = (pageIndex + 1) * pageSize < totalCount;
        if (hasNextPage)
        {
            response.NextPageToken = (pageIndex + 1).ToString();
        }

        return response;
    }

    // -----------------------------------------------------------------------
    // CreateScenario
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public override async Task<CreateScenarioResponse> CreateScenario(
        CreateScenarioRequest request, ServerCallContext context)
    {
        if (request.Scenario is null)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "Scenario must be provided."));
        }

        var entity = ToEntity(request.Scenario);

        // Assign a new UUID if the client did not provide one
        if (string.IsNullOrWhiteSpace(entity.ScenarioId))
        {
            entity.ScenarioId = Guid.NewGuid().ToString();
        }

        // Check for duplicate IDs
        bool exists = await _db.Scenarios
            .AnyAsync(s => s.ScenarioId == entity.ScenarioId, context.CancellationToken);
        if (exists)
        {
            throw new RpcException(
                new Status(StatusCode.AlreadyExists,
                           $"Scenario '{entity.ScenarioId}' already exists."));
        }

        _db.Scenarios.Add(entity);
        await _db.SaveChangesAsync(context.CancellationToken);

        return new CreateScenarioResponse { Scenario = ToProto(entity) };
    }

    // -----------------------------------------------------------------------
    // UpdateScenario
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public override async Task<UpdateScenarioResponse> UpdateScenario(
        UpdateScenarioRequest request, ServerCallContext context)
    {
        if (request.Scenario is null)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "Scenario must be provided."));
        }

        var existing = await _db.Scenarios
            .FirstOrDefaultAsync(s => s.ScenarioId == request.Scenario.ScenarioId,
                                 context.CancellationToken);

        if (existing is null)
        {
            throw new RpcException(
                new Status(StatusCode.NotFound,
                           $"Scenario '{request.Scenario.ScenarioId}' not found."));
        }

        existing.ScenarioName = request.Scenario.ScenarioName;
        existing.Summary = request.Scenario.Summary;

        existing.BoundingBox ??= new WargameData.Entities.BoundingBoxEntity();
        if (request.Scenario.BoundingBox is not null)
        {
            existing.BoundingBox.MinLatitude  = request.Scenario.BoundingBox.MinLatitude;
            existing.BoundingBox.MinLongitude = request.Scenario.BoundingBox.MinLongitude;
            existing.BoundingBox.MaxLatitude  = request.Scenario.BoundingBox.MaxLatitude;
            existing.BoundingBox.MaxLongitude = request.Scenario.BoundingBox.MaxLongitude;
        }

        await _db.SaveChangesAsync(context.CancellationToken);

        return new UpdateScenarioResponse { Scenario = ToProto(existing) };
    }

    // -----------------------------------------------------------------------
    // DeleteScenario
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public override async Task<DeleteScenarioResponse> DeleteScenario(
        DeleteScenarioRequest request, ServerCallContext context)
    {
        var entity = await _db.Scenarios
            .FirstOrDefaultAsync(s => s.ScenarioId == request.ScenarioId,
                                 context.CancellationToken);

        if (entity is null)
        {
            throw new RpcException(
                new Status(StatusCode.NotFound,
                           $"Scenario '{request.ScenarioId}' not found."));
        }

        _db.Scenarios.Remove(entity);
        await _db.SaveChangesAsync(context.CancellationToken);

        return new DeleteScenarioResponse { Success = true };
    }

    // -----------------------------------------------------------------------
    // Mapping helpers
    // -----------------------------------------------------------------------

    private static Scenario ToProto(ScenarioEntity entity) => new()
    {
        ScenarioId   = entity.ScenarioId,
        ScenarioName = entity.ScenarioName,
        Summary      = entity.Summary,
        BoundingBox  = new BoundingBox
        {
            MinLatitude  = entity.BoundingBox.MinLatitude,
            MinLongitude = entity.BoundingBox.MinLongitude,
            MaxLatitude  = entity.BoundingBox.MaxLatitude,
            MaxLongitude = entity.BoundingBox.MaxLongitude,
        },
    };

    private static ScenarioEntity ToEntity(Scenario proto) => new()
    {
        ScenarioId   = proto.ScenarioId,
        ScenarioName = proto.ScenarioName,
        Summary      = proto.Summary,
        BoundingBox  = proto.BoundingBox is null
            ? new BoundingBoxEntity()
            : new BoundingBoxEntity
            {
                MinLatitude  = proto.BoundingBox.MinLatitude,
                MinLongitude = proto.BoundingBox.MinLongitude,
                MaxLatitude  = proto.BoundingBox.MaxLatitude,
                MaxLongitude = proto.BoundingBox.MaxLongitude,
            },
    };
}
