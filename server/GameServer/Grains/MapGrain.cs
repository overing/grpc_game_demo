using GameCore.Models;
using Orleans.Utilities;

namespace GameServer.Grains;

[Alias("GameServer.Grains.IMapGrain")]
public interface IMapGrain : IGrainWithIntegerKey
{
    [Alias("SyncCharactersAsync")]
    ValueTask SyncCharactersAsync(string userId, IMapCharacterObserver mapCharacter, GrainCancellationToken grainCancellationToken);

    [Alias("UnsyncCharactersAsync")]
    ValueTask UnsyncCharactersAsync(IMapCharacterObserver mapCharacter);

    [Alias("MoveAsync")]
    ValueTask<CharacterData> MoveAsync(Guid userId, float x, float y, GrainCancellationToken grainCancellationToken);
}

sealed class MapGrain(
    ILogger<MapGrain> logger,
    IGrainFactory grainFactory)
    : Grain, IMapGrain
{
    readonly IDictionary<Guid, CharacterData> _characters = new Dictionary<Guid, CharacterData>();

    readonly ObserverManager<IMapCharacterObserver> _observers = new(TimeSpan.FromMinutes(3), logger);

    public async ValueTask SyncCharactersAsync(string userId, IMapCharacterObserver mapCharacter, GrainCancellationToken grainCancellationToken)
    {
        var user = grainFactory.GetGrain<IUserGrain>(userId);
        var userData = await user.GetDataAsync(grainCancellationToken);
        var character = new CharacterData(userData.ID, userData.Name, Skin: 1, new(X: 0, Y: 0));

        var exists = _characters.Values.ToList();
        _characters[userData.ID] = character;

        _observers.Subscribe(mapCharacter, mapCharacter);

        foreach (var exist in exists)
        {
            var existData = new SyncCharacterData(SyncCharacterAction.Add, exist);
#pragma warning disable CA2012 // Use ValueTasks correctly
            _observers.Notify(o => o.Receive(existData), o => o == mapCharacter);
#pragma warning restore CA2012 // Use ValueTasks correctly
        }

        var data = new SyncCharacterData(SyncCharacterAction.Add, character);
#pragma warning disable CA2012 // Use ValueTasks correctly
        _observers.Notify(o => o.Receive(data));
#pragma warning restore CA2012 // Use ValueTasks correctly
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

        var newData = data with { Position = new(x, y) };
        _characters[userId] = newData;

        var sync = new SyncCharacterData(SyncCharacterAction.Move, newData);

#pragma warning disable CA2012 // Use ValueTasks correctly
        _observers.Notify(o => o.Receive(sync));
#pragma warning restore CA2012 // Use ValueTasks correctly

        return ValueTask.FromResult(data);
    }
}
