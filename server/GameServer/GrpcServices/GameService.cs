using System.Security.Claims;
using GameCore.Protos;
using GameRepository.Repositories;
using GameServer.Grains;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

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
            context.UserState["GameUserID"] = userId;

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

    public override async Task SyncCharacters(
        IAsyncStreamReader<SyncCharactersRequest> requestStream,
        IServerStreamWriter<SyncCharactersResponse> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("SyncCharacters");

        using var cts = new GrainCancellationTokenSource();
        using (context.CancellationToken.Register(static state => ((GrainCancellationTokenSource)state!).Cancel().Ignore(), cts))
        {
            var logger = context.GetHttpContext().RequestServices.GetRequiredService<ILogger<MapCharacterObserver>>();
            var observer = new MapCharacterObserver(logger, responseStream, context.CancellationToken);
            var observerReference = _clusterClient.CreateObjectReference<IMapCharacterObserver>(observer);

            try
            {
                var map = _clusterClient.GetGrain<IMapGrain>(1);
                await foreach (var data in requestStream.ReadAllAsync(context.CancellationToken))
                {
                    var userId = data.ID;
                    var user = _clusterClient.GetGrain<IUserGrain>(data.ID);

                    await map.SyncCharactersAsync(userId, observerReference, cts.Token);
                    await user.SetMapCodeAsync(1, cts.Token);
                    _logger.LogInformation("UserId#{userId} join to map", userId);
                }
                await map.UnsyncCharactersAsync(observerReference);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

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
            var user = _clusterClient.GetGrain<IUserGrain>(userId);
            var mapCode = await user.GetMapCodeAsync(cts.Token);

            var map = _clusterClient.GetGrain<IMapGrain>(mapCode);
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
