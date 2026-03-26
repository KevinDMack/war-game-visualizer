using Grpc.Core;

namespace ScenarioService.Tests;

/// <summary>
/// Minimal <see cref="ServerCallContext"/> implementation for unit testing.
/// </summary>
internal sealed class TestServerCallContext : ServerCallContext
{
    private readonly Metadata _requestHeaders;
    private readonly CancellationToken _cancellationToken;
    private readonly Metadata _responseTrailers;
    private readonly AuthContext _authContext;
    private WriteOptions? _writeOptions;

    private TestServerCallContext(
        Metadata requestHeaders,
        CancellationToken cancellationToken)
    {
        _requestHeaders = requestHeaders;
        _cancellationToken = cancellationToken;
        _responseTrailers = [];
        _authContext = new AuthContext(null, []);
    }

    /// <summary>Creates a new <see cref="TestServerCallContext"/> for use in tests.</summary>
    public static TestServerCallContext Create(
        Metadata? requestHeaders = null,
        CancellationToken cancellationToken = default)
        => new(requestHeaders ?? [], cancellationToken);

    protected override string MethodCore => "/wargame.v1.ScenarioService/Test";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "127.0.0.1";
    protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(5);
    protected override Metadata RequestHeadersCore => _requestHeaders;
    protected override CancellationToken CancellationTokenCore => _cancellationToken;
    protected override Metadata ResponseTrailersCore => _responseTrailers;
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore
    {
        get => _writeOptions;
        set => _writeOptions = value;
    }
    protected override AuthContext AuthContextCore => _authContext;

    protected override ContextPropagationToken CreatePropagationTokenCore(
        ContextPropagationOptions? options) =>
        throw new NotImplementedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) =>
        Task.CompletedTask;
}
