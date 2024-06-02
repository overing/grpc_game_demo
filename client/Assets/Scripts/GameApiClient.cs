
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Net.Http;
using GameCore.Protocols;
using Grpc.Net.Client;

public interface IGameApiClient
{
    ValueTask<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

public sealed class GameApiClient : IGameApiClient, IDisposable
{
    static readonly string Address = "https://localhost:5000";
    bool _disposed;
    YetAnotherHttpHandler _httpHandler;
    HttpClient _httpClient;
    GrpcChannel _grpcChannel;
    readonly GameService.GameServiceClient _serviceClient;

    public GameApiClient()
    {
        _httpHandler = new YetAnotherHttpHandler { SkipCertificateVerification = true };
        _httpClient = new HttpClient(_httpHandler);
        _grpcChannel = GrpcChannel.ForAddress(Address, new GrpcChannelOptions { HttpHandler = _httpHandler });
        _serviceClient = new GameService.GameServiceClient(_grpcChannel);
    }

    public async ValueTask<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var call = _serviceClient.Login(request);
        await call.ResponseStream.MoveNext(cancellationToken);
        return call.ResponseStream.Current;
    }

    void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_grpcChannel is not null)
                {
                    _grpcChannel.Dispose();
                    _grpcChannel = null;
                }
                if (_httpClient is not null)
                {
                    _httpClient.Dispose();
                    _httpClient = null;
                }
                if (_httpHandler is not null)
                {
                    _httpHandler.Dispose();
                    _httpHandler = null;
                }
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
