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
    ValueTask JoinAsync(UserData userData, GrainCancellationToken grainCancellationToken);

    [Alias("ChangeNameAsync")]
    ValueTask ChangeNameAsync(Guid userId, string newName, GrainCancellationToken grainCancellationToken);

    [Alias("ChangeSkinAsync")]
    ValueTask ChangeSkinAsync(Guid userId, byte newSkin, GrainCancellationToken grainCancellationToken);

    [Alias("MoveAsync")]
    ValueTask<CharacterData> MoveAsync(Guid userId, float x, float y, GrainCancellationToken grainCancellationToken);

    [Alias("LeaveAsync")]
    ValueTask LeaveAsync(Guid userId, GrainCancellationToken grainCancellationToken);
}

sealed class MapGrain(
    ILogger<MapGrain> logger,
    ILogger<ObserverManager<IMapCharacterObserver>> observerManagerLogger)
    : Grain, IMapGrain
{
    readonly Dictionary<Guid, CharacterData> _characters = [];

    readonly ObserverManager<IMapCharacterObserver> _characterObservers = new(TimeSpan.FromMinutes(3), observerManagerLogger);

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

    public ValueTask JoinAsync(UserData userData, GrainCancellationToken grainCancellationToken)
    {
        if (!_characters.ContainsKey(userData.ID))
            _characters[userData.ID] = new CharacterData(
                userData.ID,
                userData.Name,
                Skin: userData.Skin,
                Position: new(X: userData.PosX, Y: userData.PosY));

        foreach (var characterDatas in _characters.Values)
        {
            var existData = new SyncCharacterData(SyncCharacterAction.Add, characterDatas);
            _characterObservers.NotifyIgnoreWarning(o => o.Receive(existData));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ChangeNameAsync(Guid userId, string newName, GrainCancellationToken grainCancellationToken)
    {
        if (!_characters.TryGetValue(userId, out var originalCharacterData))
            throw new ArgumentException($"Character#{userId:N} not found", nameof(userId));

        var characterData = originalCharacterData with
        {
            Name = newName,
        };

        _characters[userId] = characterData;

        var data = new SyncCharacterData(SyncCharacterAction.ChangeName, characterData);

        _characterObservers.NotifyIgnoreWarning(o => o.Receive(data));

        return ValueTask.CompletedTask;
    }

    public ValueTask ChangeSkinAsync(Guid userId, byte newSkin, GrainCancellationToken grainCancellationToken)
    {
        if (!_characters.TryGetValue(userId, out var originalCharacterData))
            throw new ArgumentException($"Character#{userId:N} not found", nameof(userId));

        var characterData = originalCharacterData with
        {
            Skin = newSkin,
        };

        _characters[userId] = characterData;

        var data = new SyncCharacterData(SyncCharacterAction.ChangeSkin, characterData);

        _characterObservers.NotifyIgnoreWarning(o => o.Receive(data));

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

        _characters.Remove(userId);

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
        var response = new SyncCharactersResponse { ID = characterData.ID.ToString("N") };
        switch (data.Action)
        {
            case SyncCharacterAction.Add:
                response.Join = new()
                {
                    Name = characterData.Name,
                    Skin = characterData.Skin,
                    Position = characterData.Position.ToVector2(),
                };
                break;

            case SyncCharacterAction.Delete:
                response.Leave = new();
                break;

            case SyncCharacterAction.Move:
                response.Move = characterData.Position.ToVector2();
                break;

            case SyncCharacterAction.ChangeName:
                response.Rename = characterData.Name;
                break;

            case SyncCharacterAction.ChangeSkin:
                response.Skin = characterData.Skin;
                break;
        }
        if (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Write sync character canceled");
            return;
        }
        try
        {
            await responseStream.WriteAsync(response, cancellationToken);
            logger.LogInformation("Write sync character: {data}", data);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Write sync character canceled");
        }
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
