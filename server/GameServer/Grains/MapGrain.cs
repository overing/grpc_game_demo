using GameCore.Models;
using GameCore.Protos;
using Grpc.Core;
using Orleans.Utilities;

namespace GameServer.Grains;

[Alias("GameServer.Grains.IMapGrain")]
public interface IMapGrain : IGrainWithIntegerKey
{
    [Alias("SubscribeAsync")]
    ValueTask SubscribeAsync(IMapCharacterObserver mapCharacter);

    [Alias("UnsubscribeAsync")]
    ValueTask UnsubscribeAsync(IMapCharacterObserver mapCharacter);

    [Alias("JoinAsync")]
    ValueTask JoinAsync(Guid userId, GrainCancellationToken grainCancellationToken);

    [Alias("MoveAsync")]
    ValueTask<CharacterData> MoveAsync(Guid userId, float x, float y, GrainCancellationToken grainCancellationToken);

    [Alias("LeaveAsync")]
    ValueTask LeaveAsync(Guid userId, GrainCancellationToken grainCancellationToken);
}

sealed class MapGrain(
    ILogger<MapGrain> logger,
    IGrainFactory grainFactory)
    : Grain, IMapGrain
{
    readonly Dictionary<Guid, CharacterData> _characters = [];

    readonly ObserverManager<IMapCharacterObserver> _characterObservers = new(TimeSpan.FromMinutes(3), logger);

    readonly Queue<ChatData> _chats = [];

    readonly ObserverManager<IMapChatObserver> _chatObservers = new(TimeSpan.FromMinutes(3), logger);

    public ValueTask SubscribeAsync(IMapCharacterObserver mapCharacter)
    {
        logger.LogInformation(nameof(SubscribeAsync));
        _characterObservers.Subscribe(mapCharacter, mapCharacter);
        return ValueTask.CompletedTask;
    }

    public ValueTask UnsubscribeAsync(IMapCharacterObserver mapCharacter)
    {
        _characterObservers.Unsubscribe(mapCharacter);
        return ValueTask.CompletedTask;
    }

    public async ValueTask JoinAsync(Guid userId, GrainCancellationToken grainCancellationToken)
    {
        if (!_characters.ContainsKey(userId))
        {
            var user = grainFactory.GetGrain<IUserGrain>(userId);
            var userData = await user.GetDataAsync(grainCancellationToken);
            _characters[userId] = new CharacterData(userId, userData.Name, Skin: 1, Position: new(X: 0, Y: 0));
        }

        foreach (var characterDatas in _characters.Values)
        {
            var existData = new SyncCharacterData(SyncCharacterAction.Add, characterDatas);
            _characterObservers.NotifyIgnoreWarning(o => o.Receive(existData));
        }
    }

    public ValueTask<CharacterData> MoveAsync(Guid userId, float x, float y, GrainCancellationToken grainCancellationToken)
    {
        if (!_characters.TryGetValue(userId, out var chatacterData))
            throw new ArgumentException($"Character#{userId:N} not found", nameof(userId));

        var newCharacterData = chatacterData with { Position = new(x, y) };
        _characters[userId] = newCharacterData;

        var data = new SyncCharacterData(SyncCharacterAction.Move, newCharacterData);

        _characterObservers.NotifyIgnoreWarning(o => o.Receive(data));

        return ValueTask.FromResult(chatacterData);
    }

    public async ValueTask LeaveAsync(Guid userId, GrainCancellationToken grainCancellationToken)
    {
        if (!_characters.TryGetValue(userId, out var characterData))
        {
            var user = grainFactory.GetGrain<IUserGrain>(userId);
            var userData = await user.GetDataAsync(grainCancellationToken);
            characterData = new CharacterData(userId, userData.Name, Skin: 1, Position: new(X: 0, Y: 0));
        }
        else
            _characters.Remove(userId);

        var data = new SyncCharacterData(SyncCharacterAction.Delete, characterData);
        _characterObservers.NotifyIgnoreWarning(o => o.Receive(data));
    }
}

[Alias("GameServer.Grains.IMapCharacterObserver")]
public interface IMapCharacterObserver : IGrainObserver
{
    [Alias("Receive")]
    ValueTask Receive(SyncCharacterData data);
}

sealed class MapCharacterObserver(
    ILogger<MapCharacterObserver> logger,
    IServerStreamWriter<SyncCharactersResponse> responseStream,
    CancellationToken cancellationToken = default) : IMapCharacterObserver
{
    public async ValueTask Receive(SyncCharacterData data)
    {
        var characterData = data.Character;
        var response = new SyncCharactersResponse
        {
            Action = (int)data.Action,
            Character = new Character
            {
                ID = characterData.ID.ToString("N"),
                Name = characterData.Name,
                Skin = characterData.Skin,
                Position = new Vector2
                {
                    X = characterData.Position.X,
                    Y = characterData.Position.Y,
                },
            },
        };
        await responseStream.WriteAsync(response, cancellationToken);
        logger.LogInformation("Write sync character: {data}", data);
    }
}

static class ObserverManagerExtensions
{
    public static void NotifyIgnoreWarning<TObserver>(
        this ObserverManager<TObserver> manager,
        Func<TObserver, ValueTask> notification,
        Func<TObserver, bool>? predicate = null)
    {
#pragma warning disable CA2012 // Use ValueTasks correctly
        manager.Notify(o => notification(o), predicate);
#pragma warning restore CA2012 // Use ValueTasks correctly
    }
}
