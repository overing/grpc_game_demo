using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameCore.Models;
using GameCore.Protos;
using Grpc.Core;

namespace GameClient;

public interface IGameApiClient
{
    ValueTask<LoginData> LoginAsync(string account, CancellationToken cancellationToken = default);
    ValueTask<EchoData> EchoAsync(DateTimeOffset clientTime, CancellationToken cancellationToken = default);
    ValueTask MoveAsync(float x, float y, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ChatData> SyncChatAsync(Guid userId, CancellationToken cancellationToken = default);
    ValueTask ChatAsync(string message, CancellationToken cancellationToken = default);
    ValueTask<UserData> ChangeNameAsync(string newName, CancellationToken cancellationToken = default);
    ValueTask<UserData> ChangeSkinAsync(byte newSkin, CancellationToken cancellationToken = default);
    ValueTask LogoutAsync(CancellationToken cancellationToken = default);

    GameService.GameServiceClient Client { get; }
}

sealed partial class GameApiClient : IGameApiClient, IDisposable
{
    bool _disposed;
    ChannelBase? _channel;
    readonly bool _disposeChannel;
    readonly GameService.GameServiceClient _client;

    public GameService.GameServiceClient Client => _client;

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
}
