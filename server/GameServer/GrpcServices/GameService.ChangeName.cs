using System.Security.Claims;
using GameCore.Protos;
using GameServer.Grains;
using Grpc.Core;

namespace GameServer.GrpcServices;

public sealed partial class GameService
{
    public override async Task<User> ChangeName(ChangeNameRequest request, ServerCallContext context)
    {
        _logger.LogInformation("ChangeName: {request.NewName}", request.NewName);

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
            var userId = Guid.Parse(rawUserId);

            var user = _clusterClient.GetGrain<IUserGrain>(userId);
            var userData = await user.ChangeNameAsync(request.NewName, gcts.Token);

            var chatRoom = _clusterClient.GetGrain<IChatRoomGrain>(ChatRoomID);
            await chatRoom.ChangeNameAsync(userId, request.NewName, gcts.Token);

            var map = _clusterClient.GetGrain<IMapGrain>(ChatRoomID);
            await map.ChangeNameAsync(userId, request.NewName, gcts.Token);

            return new()
            {
                ID = userData.ID.ToString("N"),
                Name = userData.Name,
                Email = userData.Email,
                Skin = userData.Skin,
                Position = new() { X = userData.PosX, Y = userData.PosY },
            };
        }
    }
}
