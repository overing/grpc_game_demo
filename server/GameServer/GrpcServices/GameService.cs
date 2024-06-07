using Grpc.Core;
using GameCore.Protos;
using GameServer.Grains;
using Google.Protobuf.WellKnownTypes;

namespace GameServer.GrpcServices;

public sealed class GameService(
    ILogger<GameService> logger,
    IClusterClient clusterClient)
    : GameCore.Protos.GameService.GameServiceBase
{
    readonly ILogger<GameService> _logger = logger;

    public override async Task Login(LoginRequest request, IServerStreamWriter<LoginResponse> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("Login: {request.UserId}", request.UserId);

        using var cts = new GrainCancellationTokenSource();
        using (context.CancellationToken.Register(static state => ((GrainCancellationTokenSource)state!).Cancel().Ignore(), cts))
        {
            var user = clusterClient.GetGrain<IUserGrain>(request.UserId);
            var data = await user.LoginAsync(cts.Token);
            var response = new LoginResponse
            {
                ServerTime = data.ServerTime.ToTimestamp(),
                ServerOffet = data.ServerTime.Offset.ToDuration(),
                User = new User
                {
                    ID = data.User.ID.ToString("N"),
                    Name = data.User.Name,
                    Email = data.User.Email,
                },
            };
            await responseStream.WriteAsync(response);
        }
    }
}
