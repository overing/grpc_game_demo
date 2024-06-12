using GameCore.Models;
using GameCore.Protos;
using Grpc.Core;
using Orleans.Utilities;

namespace GameServer.Grains;

[Alias("GameServer.Grains.IMapGrain")]
public interface IMapGrain : IGrainWithIntegerKey
{
    [Alias("SubscribeCharacterAsync")]
    ValueTask SubscribeCharacterAsync(Guid userId, IMapCharacterObserver mapCharacter, GrainCancellationToken grainCancellationToken);

    [Alias("UnsubscribeCharacterAsync")]
    ValueTask UnsubscribeCharacterAsync(IMapCharacterObserver mapCharacter);

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

    public async ValueTask SubscribeCharacterAsync(Guid userId, IMapCharacterObserver mapCharacter, GrainCancellationToken grainCancellationToken)
    {
        var user = grainFactory.GetGrain<IUserGrain>(userId);
        var userData = await user.GetDataAsync(grainCancellationToken);
        var newCharacterData = new CharacterData(userData.ID, userData.Name, Skin: 1, new(X: 0, Y: 0));

        var existsCharacterDatas = _characters.Values.ToList();
        _characters[userData.ID] = newCharacterData;

        _characterObservers.Subscribe(mapCharacter, mapCharacter);

        foreach (var characterDatas in existsCharacterDatas)
        {
            var existData = new SyncCharacterData(SyncCharacterAction.Add, characterDatas);
            _characterObservers.NotifyIgnoreWarning(o => o.Receive(existData), o => o == mapCharacter);
        }

        var data = new SyncCharacterData(SyncCharacterAction.Add, newCharacterData);

        _characterObservers.NotifyIgnoreWarning(o => o.Receive(data));
    }

    public ValueTask UnsubscribeCharacterAsync(IMapCharacterObserver mapCharacter)
    {
        _characterObservers.Unsubscribe(mapCharacter);
        return ValueTask.CompletedTask;
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

    public ValueTask LeaveAsync(Guid userId, GrainCancellationToken grainCancellationToken)
    {
        if (!_characters.TryGetValue(userId, out var characterData))
            throw new ArgumentException($"Character#{userId:N} not found", nameof(userId));

        var data = new SyncCharacterData(SyncCharacterAction.Delete, characterData);

        _characterObservers.NotifyIgnoreWarning(o => o.Receive(data));

        return ValueTask.CompletedTask;
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
