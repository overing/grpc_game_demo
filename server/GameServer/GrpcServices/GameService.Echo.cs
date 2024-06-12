using System.Security.Claims;
using GameCore.Protos;
using GameServer.Grains;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace GameServer.GrpcServices;

public sealed partial class GameService
{
    public override async Task<EchoResponse> Echo(EchoRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Echo: {request.ClientTime}", request.ClientTime);

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
            var user = _clusterClient.GetGrain<IUserGrain>(Guid.Parse(userId));
            var clientTime = request.ClientTime.ToDateTimeOffset();
            var gatewayTime = _timeProvider.GetLocalNow();
            var data = await user.EchoAsync(clientTime, gatewayTime, cts.Token);

            var response = new EchoResponse
            {
                ClientToGateway = data.ClientToGateway.ToDuration(),
                GatewayToSilo = data.GatewayToSilo.ToDuration(),
                SiloToGateway = (_timeProvider.GetLocalNow() - data.SiloTime).ToDuration(),
                SiloTime = data.SiloTime.ToTimeOffset(),
            };
            return response;
        }
    }
}
