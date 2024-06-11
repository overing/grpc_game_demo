using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
    IAsyncEnumerable<SyncCharacterData> SyncCharactersAsync(CancellationToken cancellationToken = default);
    ValueTask<CharacterData> MoveAsync(float x, float y, CancellationToken cancellationToken = default);
    ValueTask LogoutAsync(CancellationToken cancellationToken = default);
    event Action<string> Log;
}

sealed class GameApiClient : IGameApiClient, IDisposable
{
    bool _disposed;
    ChannelBase? _channel;
    readonly bool _disposeChannel;
    readonly GameService.GameServiceClient _client;

    public event Action<string>? Log;

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

    public async ValueTask<LoginData> LoginAsync(string account, CancellationToken cancellationToken = default)
    {
        if (account is null)
            throw new ArgumentNullException(nameof(account));
        if (!Guid.TryParse(account, out _))
            throw new ArgumentException("Require guid format", nameof(account));

        var request = new LoginRequest { Account = account };

        var response = await _client.LoginAsync(request, cancellationToken: cancellationToken);

        var user = new UserData(
            ID: Guid.Parse(response.User.ID),
            Name: response.User.Name,
            Email: response.User.Email);

        var data = new LoginData(
            ServerTime: response.ServerTime.ToDateTimeOffset(),
            User: user);

        return data;
    }

    public async ValueTask<EchoData> EchoAsync(DateTimeOffset clientTime, CancellationToken cancellationToken = default)
    {
        var request = new EchoRequest { ClientTime = clientTime.ToTimeOffset() };

        var response = await _client.EchoAsync(request, cancellationToken: cancellationToken);

        var data = new EchoData(
            ClientToGateway: response.ClientToGateway.ToTimeSpan(),
            GatewayToSilo: response.GatewayToSilo.ToTimeSpan(),
            SiloToGateway: response.SiloToGateway.ToTimeSpan(),
            SiloTime: response.SiloTime.ToDateTimeOffset());

        return data;
    }

    public async IAsyncEnumerable<SyncCharacterData> SyncCharactersAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new SyncCharactersRequest { MapCode = 1 };

        Log?.Invoke("SyncCharactersAsync 1");
        using var call = _client.SyncCharacters(request, cancellationToken: cancellationToken);
        Log?.Invoke("SyncCharactersAsync 2");
        while (await call.ResponseStream.MoveNext())
        {
            Log?.Invoke("SyncCharactersAsync 3");
            var item = call.ResponseStream.Current;
            var character = item.Character;

            var characterData = new CharacterData(
                ID: Guid.Parse(character.ID),
                Name: character.Name,
                Skin: character.Skin,
                Position: (character.Position.X, character.Position.Y));
            var data = new SyncCharacterData(
                Action: (SyncCharacterAction)item.Action,
                Character: characterData);

            yield return data;
        }
        Log?.Invoke("SyncCharactersAsync 4");
    }

    public async ValueTask<CharacterData> MoveAsync(float x, float y, CancellationToken cancellationToken = default)
    {
        var request = new MoveRequest { Position = new Vector2 { X = x, Y = y } };

        var response = await _client.MoveAsync(request, cancellationToken: cancellationToken);

        var character = response.Character;

        var data = new CharacterData(
                ID: Guid.Parse(character.ID),
                Name: character.Name,
                Skin: character.Skin,
                Position: (character.Position.X, character.Position.Y));

        return data;
    }

    public async ValueTask LogoutAsync(CancellationToken cancellationToken = default)
    {
        _ = await _client.LogoutAsync(new(), cancellationToken: cancellationToken);
    }
}
