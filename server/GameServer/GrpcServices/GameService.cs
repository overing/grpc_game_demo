using Grpc.Core;
using GameCore.Protos;
using Google.Protobuf.WellKnownTypes;

namespace GameServer.GrpcServices;

public sealed class GameService(ILogger<GameService> logger) : GameCore.Protos.GameService.GameServiceBase
{
    readonly ILogger<GameService> _logger = logger;

    public override async Task Login(LoginRequest request, IServerStreamWriter<LoginResponse> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("Login: {request.UserId}", request.UserId);

        await responseStream.WriteAsync(new()
        {
            ServerTime = DateTimeOffset.UtcNow.ToTimestamp(),
            Name = "guest-" + request.UserId,
        });
    }
}
