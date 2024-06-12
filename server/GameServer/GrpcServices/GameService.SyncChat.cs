using GameCore.Protos;
using GameServer.Grains;
using Grpc.Core;

namespace GameServer.GrpcServices;

public sealed partial class GameService
{
    const int ChatRoomID = 1;

    public override async Task SyncChat(
        IAsyncStreamReader<SyncChatRequest> requestStream,
        IServerStreamWriter<SyncChatResponse> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("SyncChat");

        using var cts = new GrainCancellationTokenSource();
        using (context.CancellationToken.Register(static state => ((GrainCancellationTokenSource)state!).Cancel().Ignore(), cts))
        {
            var logger = context.GetHttpContext().RequestServices.GetRequiredService<ILogger<MapChatObserver>>();
            var observer = new MapChatObserver(logger, responseStream, context.CancellationToken);
            var observerReference = _clusterClient.CreateObjectReference<IMapChatObserver>(observer);

            try
            {
                var chatRoom = _clusterClient.GetGrain<IChatRoomGrain>(ChatRoomID);
                await foreach (var data in requestStream.ReadAllAsync(context.CancellationToken))
                {
                    var userId = Guid.Parse(data.ID);
                    var user = _clusterClient.GetGrain<IUserGrain>(userId);

                    await chatRoom.SubscribeChatAsync(userId, observerReference, cts.Token);
                    _logger.LogInformation("UserId#{userId} join to chat", userId);
                }
                await chatRoom.UnsubscribeChatAsync(observerReference);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
