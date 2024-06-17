using System.Security.Claims;
using GameCore.Protos;
using GameServer.Grains;
using Grpc.Core;

namespace GameServer.GrpcServices;

public sealed partial class GameService
{
    public override async Task<ChatResponse> Chat(ChatRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Chat: {request.Position}", new { request.Message });

        var identity = context.GetHttpContext().User.Identity;
        if (identity is not ClaimsIdentity id ||
            id.Claims.FirstOrDefault(c => c.Type == "GameUserID") is not Claim claim ||
            claim.Value is not string rawUserId)
        {
            context.Status = new Status(StatusCode.Unauthenticated, "Need login.");
            return new();
        }

        using var gcts = new GrainCancellationTokenSource();
        using (context.CancellationToken.Register(static state => ((GrainCancellationTokenSource)state!).Cancel().Ignore(), gcts))
        {
            var chatRoom = _clusterClient.GetGrain<IChatRoomGrain>(ChatRoomID);
            await chatRoom.ChatAsync(Guid.Parse(rawUserId), request.Message, gcts.Token);

            return new();
        }
    }
}
