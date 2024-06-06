using System;
using System.Threading;
using System.Threading.Tasks;
using GameCore.Models;
using GameCore.Protos;
using Grpc.Core;

namespace GameClient
{
    sealed class GameApiClient : IGameApiClient, IDisposable
    {

        bool _disposed;
        ChannelBase? _channel;
        bool _disposeChannel;
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
                if (_disposeChannel && _channel is IDisposable disposable)
                {
                    disposable.Dispose();
                    _channel = null;
                }

                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        public async ValueTask<PlayerData> LoginAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (!Guid.TryParse(userId, out _))
                throw new ArgumentException("Require guid format", nameof(userId));

            var call = _client.Login(new LoginRequest { UserId = userId });
            await call.ResponseStream.MoveNext(cancellationToken);
            var response = call.ResponseStream.Current;

            return new PlayerData
            {
                ServerTime = response.ServerTime.ToDateTimeOffset(),
                Name = response.Name,
            };
        }
    }
}
