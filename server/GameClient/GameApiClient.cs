using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    IAsyncEnumerable<SyncCharacterData> SyncCharactersAsync(Guid userId, CancellationToken cancellationToken = default);
    ValueTask MoveAsync(float x, float y, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ChatData> SyncChatAsync(Guid userId, CancellationToken cancellationToken = default);
    ValueTask ChatAsync(string message, CancellationToken cancellationToken = default);
    ValueTask LogoutAsync(CancellationToken cancellationToken = default);
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

    public async IAsyncEnumerable<SyncCharacterData> SyncCharactersAsync(Guid userId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new SyncCharactersRequest { ID = userId.ToString("N") };

        using var duplex = _client.SyncCharacters(cancellationToken: cancellationToken);
        _ = ResendAsync(duplex, request, cancellationToken);
        await foreach (var response in duplex.ResponseStream.ReadAllAsync(cancellationToken))
        {
            var character = response.Character;

            var characterData = new CharacterData(
                ID: Guid.Parse(character.ID),
                Name: character.Name,
                Skin: character.Skin,
                Position: new(character.Position.X, character.Position.Y));
            var data = new SyncCharacterData(
                Action: (SyncCharacterAction)response.Action,
                Character: characterData);

            yield return data;
        }

        static async ValueTask ResendAsync(AsyncDuplexStreamingCall<SyncCharactersRequest, SyncCharactersResponse> duplex, SyncCharactersRequest request, CancellationToken cancellationToken)
        {
            var sw = new Stopwatch();
            while (!cancellationToken.IsCancellationRequested)
            {
                await duplex.RequestStream.WriteAsync(request);

                sw.Restart();
                while (sw.Elapsed.TotalMinutes < 1 && !cancellationToken.IsCancellationRequested)
                    await Task.Yield();
            }
        }
    }

    public async ValueTask MoveAsync(float x, float y, CancellationToken cancellationToken = default)
    {
        var request = new MoveRequest { Position = new Vector2 { X = x, Y = y } };

        await _client.MoveAsync(request, cancellationToken: cancellationToken);
    }

    public async IAsyncEnumerable<ChatData> SyncChatAsync(Guid userId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new SyncChatRequest { ID = userId.ToString("N") };

        using var duplex = _client.SyncChat(cancellationToken: cancellationToken);
        await duplex.RequestStream.WriteAsync(request);
        await foreach (var response in duplex.ResponseStream.ReadAllAsync(cancellationToken))
        {
            var data = new ChatData(
                Sender: response.Sender,
                Message: response.Message);

            yield return data;
        }
    }

    public async ValueTask ChatAsync(string message, CancellationToken cancellationToken = default)
    {
        var request = new ChatRequest { Message = message };

        await _client.ChatAsync(request, cancellationToken: cancellationToken);
    }

    public async ValueTask LogoutAsync(CancellationToken cancellationToken = default)
    {
        _ = await _client.LogoutAsync(new(), cancellationToken: cancellationToken);
    }
}
