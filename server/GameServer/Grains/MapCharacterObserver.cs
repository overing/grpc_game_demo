using System.Threading.Channels;
using GameCore.Models;

namespace GameServer.Grains;

[Alias("GameServer.Grains.IMapCharacterObserver")]
public interface IMapCharacterObserver : IGrainObserver
{
    [Alias("Receive")]
    ValueTask Receive(SyncCharacterData data);

    [Alias("Get")]
    ValueTask<CharacterData> Get();
}

sealed class MapCharacterObserver(UserData userData) : IMapCharacterObserver, IAsyncEnumerable<SyncCharacterData>
{
    readonly Channel<SyncCharacterData> _channel = Channel.CreateUnbounded<SyncCharacterData>();

    public async IAsyncEnumerator<SyncCharacterData> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var data in _channel.Reader.ReadAllAsync(cancellationToken))
            yield return data;
    }

    public ValueTask Receive(SyncCharacterData data)
        => _channel.Writer.WriteAsync(data);

    public ValueTask<CharacterData> Get()
        => ValueTask.FromResult(new CharacterData(
            ID: userData.ID,
            Name: userData.Name,
            Skin: 1,
            Position: (0, 0)));
}
