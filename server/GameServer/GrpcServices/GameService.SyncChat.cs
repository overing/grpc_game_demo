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
            var chatRoom = _clusterClient.GetGrain<IChatRoomGrain>(ChatRoomID);

            Guid? userId = null;
            try
            {
                bool joined = false;
                await foreach (var data in requestStream.ReadAllAsync(context.CancellationToken))
                {
                    var id = Guid.Parse(data.ID);
                    userId = id;
                    var user = _clusterClient.GetGrain<IUserGrain>(id);

                    await chatRoom.SubscribeAsync(observerReference);
                    if (joined)
                        continue;

                    await chatRoom.JoinAsync(id, cts.Token);
                    joined = true;
                    _logger.LogInformation("UserId#{userId} join to chat", id);
                }
            }
            catch (OperationCanceledException)
            {
                if (userId is Guid id)
                    await chatRoom.LeaveAsync(id, cts.Token);
                await chatRoom.UnsubscribeAsync(observerReference);
            }
        }
    }
}
