using GameCore.Models;
using Orleans.Utilities;

namespace GameServer.Grains;

[Alias("GameServer.Grains.IMapGrain")]
public interface IMapGrain : IGrainWithIntegerKey
{
    [Alias("SyncCharactersAsync")]
    ValueTask SyncCharactersAsync(IMapCharacterObserver mapCharacter);

    [Alias("UnsyncCharactersAsync")]
    ValueTask UnsyncCharactersAsync(IMapCharacterObserver mapCharacter);

    [Alias("MoveAsync")]
    ValueTask<CharacterData> MoveAsync(Guid userId, float x, float y, GrainCancellationToken grainCancellationToken);
}

sealed class MapGrain(
    ILogger<MapGrain> logger)
    : Grain, IMapGrain
{
    readonly IDictionary<Guid, CharacterData> _characters = new Dictionary<Guid, CharacterData>();

    readonly ObserverManager<IMapCharacterObserver> _observers = new(TimeSpan.FromMinutes(3), logger);

    public async ValueTask SyncCharactersAsync(IMapCharacterObserver mapCharacter)
    {
        var character = await mapCharacter.Get();
        _characters.Add(character.ID, character);

        var data = new SyncCharacterData(SyncCharacterAction.Add, character);

#pragma warning disable CA2012 // Use ValueTasks correctly
        _observers.Notify(o => o.Receive(data));
#pragma warning restore CA2012 // Use ValueTasks correctly

        _observers.Subscribe(mapCharacter, mapCharacter);
    }

    public ValueTask UnsyncCharactersAsync(IMapCharacterObserver mapCharacter)
    {
        _observers.Unsubscribe(mapCharacter);
        return ValueTask.CompletedTask;
    }

    public ValueTask<CharacterData> MoveAsync(Guid userId, float x, float y, GrainCancellationToken grainCancellationToken)
    {
        if (!_characters.TryGetValue(userId, out var data))
            throw new ArgumentException($"Character#{userId:N} not found", nameof(userId));

        data = data with
        {
            Position = (x, y),
        };
        _characters[userId] = data;

        var sync = new SyncCharacterData(SyncCharacterAction.Add, data);

#pragma warning disable CA2012 // Use ValueTasks correctly
        _observers.Notify(o => o.Receive(sync));
#pragma warning restore CA2012 // Use ValueTasks correctly

        return ValueTask.FromResult(data);
    }
}
