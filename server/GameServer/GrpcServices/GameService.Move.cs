using System.Security.Claims;
using GameCore.Protos;
using GameServer.Grains;
using Grpc.Core;

namespace GameServer.GrpcServices;

public sealed partial class GameService
{
    public override async Task<MoveResponse> Move(MoveRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Move: {request.Position}", new { request.Position.X, request.Position.Y });

        var identity = context.GetHttpContext().User.Identity;
        if (identity is not ClaimsIdentity id ||
            id.Claims.FirstOrDefault(c => c.Type == "GameUserID") is not Claim claim ||
            claim.Value is not string userId)
        {
            context.Status = new Status(StatusCode.Unauthenticated, "Need login.");
            return new();
        }

        using var cts = new GrainCancellationTokenSource();
        using (context.CancellationToken.Register(static state => ((GrainCancellationTokenSource)state!).Cancel().Ignore(), cts))
        {
            var map = _clusterClient.GetGrain<IMapGrain>(MapID);
            var characterData = await map.MoveAsync(Guid.Parse(userId), request.Position.X, request.Position.Y, cts.Token);

            var response = new MoveResponse
            {
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
            return response;
        }
    }
}
