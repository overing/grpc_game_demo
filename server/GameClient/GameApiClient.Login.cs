using System;
using System.Threading;
using System.Threading.Tasks;
using GameCore.Models;
using GameCore.Protos;

namespace GameClient;

sealed partial class GameApiClient
{
    public async ValueTask<LoginData> LoginAsync(string account, CancellationToken cancellationToken = default)
    {
        if (account is null)
            throw new ArgumentNullException(nameof(account));
        if (!Guid.TryParse(account, out _))
            throw new ArgumentException("Require guid format", nameof(account));
        var request = new LoginRequest { Account = account };
        var response = await _client.LoginAsync(request, cancellationToken: cancellationToken);
        return new(
            ServerTime: response.ServerTime.ToDateTimeOffset(),
            User: new(
                ID: Guid.Parse(response.User.ID),
                Name: response.User.Name,
                Email: response.User.Email,
                Skin: (byte)response.User.Skin,
                PosX: response.User.Position.X,
                PosY: response.User.Position.Y));
    }
}
