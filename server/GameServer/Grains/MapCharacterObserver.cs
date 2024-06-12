using GameCore.Models;
using GameCore.Protos;
using Grpc.Core;

namespace GameServer.Grains;

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
        var item = new SyncCharactersResponse
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
        await responseStream.WriteAsync(item, cancellationToken);
        logger.LogInformation("Write sync character: {data}", data);
    }
}
