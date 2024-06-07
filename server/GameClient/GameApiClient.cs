using System;
using System.Threading;
using System.Threading.Tasks;
using GameCore.Models;
using GameCore.Protos;
using Grpc.Core;

namespace GameClient;

public interface IGameApiClient
{
    ValueTask<LoginData> LoginAsync(string userId, CancellationToken cancellationToken = default);
}

sealed class GameApiClient : IGameApiClient, IDisposable
{
    bool _disposed;
    ChannelBase? _channel;
    readonly bool _disposeChannel;
    readonly GameService.GameServiceClient _client;

    public GameApiClient(ChannelBase channel, bool disposeChannel)
    {
        _channel = channel;
        _disposeChannel = disposeChannel;
        _client = new GameService.GameServiceClient(_channel);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_disposeChannel && _channel is IDisposable disposableChannel)
            {
                disposableChannel.Dispose();
                _channel = null;
            }

            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    public async ValueTask<LoginData> LoginAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
            throw new ArgumentNullException(nameof(userId));
        if (!Guid.TryParse(userId, out _))
            throw new ArgumentException("Require guid format", nameof(userId));

        var call = _client.Login(new LoginRequest { UserId = userId });
        await call.ResponseStream.MoveNext(cancellationToken);

        var response = call.ResponseStream.Current;

        var serverTime = response.ServerTime.ToDateTimeOffset();
        serverTime = serverTime.ToOffset(response.ServerOffet.ToTimeSpan());

        var user = new UserData(
            ID: Guid.Parse(response.User.ID),
            Name: response.User.Name,
            Email: response.User.Email);

        var reply = new LoginData(
            ServerTime: serverTime,
            User: user);

        return reply;
    }
}
