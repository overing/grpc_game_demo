using Grpc.Core;
using GameCore.Protos;
using Google.Protobuf.WellKnownTypes;
using GameRepository;

namespace GameServer.GrpcServices;

public sealed class GameService(
    ILogger<GameService> logger,
    IUserRepository userRepository)
    : GameCore.Protos.GameService.GameServiceBase
{
    readonly ILogger<GameService> _logger = logger;

    public override async Task Login(LoginRequest request, IServerStreamWriter<LoginResponse> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("Login: {request.UserId}", request.UserId);

        var user = await userRepository.GetWithAccountAsync(request.UserId, context.CancellationToken);
        user ??= await userRepository.CreateAsync(request.UserId, "Guest-" + request.UserId, "none@none", context.CancellationToken);

        var now = DateTimeOffset.Now;
        await responseStream.WriteAsync(new()
        {
            ServerTime = now.ToTimestamp(),
            ServerOffet = now.Offset.ToDuration(),
            User = new()
            {
                ID = user.ID.ToString("N"),
                Name = user.Name,
                Email = user.Email,
            },
        });
    }
}
