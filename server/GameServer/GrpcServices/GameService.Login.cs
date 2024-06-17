using System.Security.Claims;
using GameCore.Protos;
using GameServer.Grains;
using Grpc.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GameServer.GrpcServices;

public sealed partial class GameService
{
    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Login: {request.Account}", request.Account);

        if (string.IsNullOrWhiteSpace(request.Account))
        {
            context.Status = new Status(StatusCode.InvalidArgument, "Account is required.");
            return new();
        }

        using var gcts = new GrainCancellationTokenSource();
        using (context.CancellationToken.Register(static state => ((GrainCancellationTokenSource)state!).Cancel().Ignore(), gcts))
        {
            var lobby = _clusterClient.GetGrain<ILobbyGrain>(primaryKey: default);
            var data = await lobby.LoginAsync(request.Account, gcts.Token);

            var rawUserId = data.User.ID.ToString("N");
            var sessionId = Guid.NewGuid().ToString("N");
            var claims = new[]
            {
                new Claim("GameUserID", rawUserId),
                new Claim("SessionID", sessionId),
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await context.GetHttpContext().SignInAsync(
                scheme: CookieAuthenticationDefaults.AuthenticationScheme,
                principal: new ClaimsPrincipal(claimsIdentity),
                properties: new AuthenticationProperties());
            await _sessionRepository.SetSessionAsync(rawUserId, sessionId);
            context.UserState["GameUserID"] = rawUserId;

            var response = new LoginResponse
            {
                ServerTime = data.ServerTime.ToTimeOffset(),
                User = new User
                {
                    ID = rawUserId,
                    Name = data.User.Name,
                    Email = data.User.Email,
                    Skin = data.User.Skin,
                    Position = new() { X = data.User.PosX, Y = data.User.PosY },
                },
            };
            return response;
        }
    }
}
