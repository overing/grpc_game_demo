using GameRepository.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GameServer.Extensions;

public class CustomCookieAuthenticationEvents(ISessionRepository sessionRepository) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userPrincipal = context.Principal!;

        var userId = userPrincipal.Claims
            .Where(c => c.Type == "GameUserID")
            .Select(c => c.Value)
            .FirstOrDefault();

        var sessionId = userPrincipal.Claims
            .Where(c => c.Type == "SessionID")
            .Select(c => c.Value)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(userId) ||
            string.IsNullOrEmpty(sessionId) ||
            !await sessionRepository.ValidateSessionAsync(userId, sessionId))
        {
            context.RejectPrincipal();

            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}