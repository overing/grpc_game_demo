using Grpc.Core;
using GameCore.Protos;
using GameServer.Grains;
using Google.Protobuf.WellKnownTypes;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using GameRepository.Repositories;

namespace GameServer.GrpcServices;

public sealed class GameService(
    ILogger<GameService> logger,
    ISessionRepository sessionRepository,
    IClusterClient clusterClient,
    TimeProvider timeProvider)
    : GameCore.Protos.GameService.GameServiceBase
{
    readonly ILogger<GameService> _logger = logger;
    readonly IClusterClient _clusterClient = clusterClient;

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Login: {request.Account}", request.Account);

        if (string.IsNullOrWhiteSpace(request.Account))
        {
            context.Status = new Status(StatusCode.InvalidArgument, "Account is required.");
            return new();
        }

        using var cts = new GrainCancellationTokenSource();
        using (context.CancellationToken.Register(static state => ((GrainCancellationTokenSource)state!).Cancel().Ignore(), cts))
        {
            var lobby = _clusterClient.GetGrain<ILobbyGrain>(primaryKey: default);
            var data = await lobby.LoginAsync(request.Account, cts.Token);

            var userId = data.User.ID.ToString("N");
            var sessionId = Guid.NewGuid().ToString("N");
            var claims = new[]
            {
                new Claim("GameUserID", userId),
                new Claim("SessionID", sessionId),
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await context.GetHttpContext().SignInAsync(
                scheme: CookieAuthenticationDefaults.AuthenticationScheme,
                principal: new ClaimsPrincipal(claimsIdentity),
                properties: new AuthenticationProperties());
            await sessionRepository.SetSessionAsync(userId, sessionId);

            var response = new LoginResponse
            {
                ServerTime = data.ServerTime.ToTimeOffset(),
                User = new User
                {
                    ID = userId,
                    Name = data.User.Name,
                    Email = data.User.Email,
                },
            };
            return response;
        }
    }

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
            var now = timeProvider.GetLocalNow();
            var user = _clusterClient.GetGrain<IUserGrain>(userId);
            var clientTime = request.ClientTime.ToDateTimeOffset();
            var gatewayTime = timeProvider.GetLocalNow();
            var data = await user.EchoAsync(clientTime, gatewayTime, cts.Token);

            var response = new EchoResponse
            {
                ClientToGateway = data.ClientToGateway.ToDuration(),
                GatewayToSilo = data.GatewayToSilo.ToDuration(),
                SiloToGateway = (timeProvider.GetLocalNow() - data.SiloTime).ToDuration(),
                SiloTime = data.SiloTime.ToTimeOffset(),
            };
            return response;
        }
    }

    public override async Task<Empty> Logout(Empty _, ServerCallContext context)
    {
        _logger.LogInformation("Logout");

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
            await sessionRepository.RemoveSessionAsync(userId);

            await context.GetHttpContext().SignOutAsync(scheme: CookieAuthenticationDefaults.AuthenticationScheme);
        }

        return new();
    }
}
