
using System;
using System.Threading;
using System.Threading.Tasks;
using GameCore.Protocols;
using Grpc.Net.Client;
using GrpcWebSocketBridge.Client;
using Microsoft.Extensions.Options;

public interface IGameApiClient
{
    ValueTask<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

public sealed class GameApiClientOptions
{
    public string Address { get; set; }
}

public sealed class GameApiClient : IGameApiClient, IDisposable
{
    bool _disposed;
    GrpcChannel _grpcChannel;
    readonly GameService.GameServiceClient _serviceClient;

    public GameApiClient(IOptions<GameApiClientOptions> options)
    {
        _grpcChannel = GrpcChannel.ForAddress(options.Value.Address, new()
        {
            HttpHandler = new GrpcWebSocketBridgeHandler(),
            DisposeHttpClient = true,
        });
        _serviceClient = new GameService.GameServiceClient(_grpcChannel);
    }

    public async ValueTask<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var call = _serviceClient.Login(request);
        await call.ResponseStream.MoveNext(cancellationToken);
        return call.ResponseStream.Current;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_grpcChannel is not null)
            {
                _grpcChannel.Dispose();
                _grpcChannel = null;
            }

            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
