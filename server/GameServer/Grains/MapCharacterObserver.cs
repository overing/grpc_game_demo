using System.Threading.Channels;
using GameCore.Models;

namespace GameServer.Grains;

[Alias("GameServer.Grains.IMapCharacterObserver")]
public interface IMapCharacterObserver : IGrainObserver
{
    [Alias("Receive")]
    ValueTask Receive(SyncCharacterData data);
}

sealed class MapCharacterObserver : IMapCharacterObserver, IAsyncEnumerable<SyncCharacterData>
{
    readonly Channel<SyncCharacterData> _channel = Channel.CreateUnbounded<SyncCharacterData>();

    public async IAsyncEnumerator<SyncCharacterData> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var data in _channel.Reader.ReadAllAsync(cancellationToken))
            yield return data;
    }

    public ValueTask Receive(SyncCharacterData data)
        => _channel.Writer.WriteAsync(data);
}
