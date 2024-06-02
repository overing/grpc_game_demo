using Grpc.Core;
using GameCore.Protocols;

namespace GameServer.Services;

public sealed class GameService(ILogger<GameService> logger) : GameCore.Protocols.GameService.GameServiceBase
{
    readonly ILogger<GameService> _logger = logger;

    public override async Task Login(LoginRequest request, IServerStreamWriter<LoginResponse> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("Login: {request.UserId}", request.UserId);
        await responseStream.WriteAsync(new() { Message = $"Login result! {DateTimeOffset.UtcNow}" });
    }
}
