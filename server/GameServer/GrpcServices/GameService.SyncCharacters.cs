using GameCore.Protos;
using GameServer.Grains;
using Grpc.Core;

namespace GameServer.GrpcServices;

public sealed partial class GameService
{
    const int MapID = 1;

    public override async Task SyncCharacters(
        IAsyncStreamReader<SyncCharactersRequest> requestStream,
        IServerStreamWriter<SyncCharactersResponse> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("SyncCharacters");

        using var cts = new GrainCancellationTokenSource();
        using (context.CancellationToken.Register(static state => ((GrainCancellationTokenSource)state!).Cancel().Ignore(), cts))
        {
            var logger = context.GetHttpContext().RequestServices.GetRequiredService<ILogger<MapCharacterObserver>>();
            var observer = new MapCharacterObserver(logger, responseStream, context.CancellationToken);
            var observerReference = _clusterClient.CreateObjectReference<IMapCharacterObserver>(observer);
            var map = _clusterClient.GetGrain<IMapGrain>(MapID);

            try
            {
                await foreach (var data in requestStream.ReadAllAsync(context.CancellationToken))
                {
                    var userId = Guid.Parse(data.ID);
                    var user = _clusterClient.GetGrain<IUserGrain>(userId);

                    await map.SubscribeCharacterAsync(userId, observerReference, cts.Token);
                    _logger.LogInformation("UserId#{userId} join to map", userId);
                }
                await map.UnsubscribeCharacterAsync(observerReference);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
