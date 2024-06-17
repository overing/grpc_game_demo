using System;
using System.Threading;
using System.Threading.Tasks;
using GameCore.Models;
using GameCore.Protos;

namespace GameClient;

sealed partial class GameApiClient
{
    public async ValueTask<UserData> ChangeNameAsync(string newName, CancellationToken cancellationToken = default)
    {
        var request = new ChangeNameRequest { NewName = newName };
        var data = await _client.ChangeNameAsync(request, cancellationToken: cancellationToken);
        return new(Guid.Parse(data.ID), data.Name, data.Email, (byte)data.Skin, data.Position.X, data.Position.Y);
    }
}
