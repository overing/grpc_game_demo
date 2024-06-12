using System.Security.Claims;
using GameServer.Grains;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GameServer.GrpcServices;

public sealed partial class GameService
{
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
            var map = _clusterClient.GetGrain<IMapGrain>(MapID);
            await map.LeaveAsync(Guid.Parse(userId), cts.Token);

            await _sessionRepository.RemoveSessionAsync(userId);

            await context.GetHttpContext().SignOutAsync(scheme: CookieAuthenticationDefaults.AuthenticationScheme);
        }

        return new();
    }
}
