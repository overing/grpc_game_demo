using System;
using System.Threading;
using System.Threading.Tasks;
using GameCore.Models;
using GameCore.Protos;

namespace GameClient;

sealed partial class GameApiClient
{
    public async ValueTask<UserData> ChangeSkinAsync(byte newSkin, CancellationToken cancellationToken = default)
    {
        var request = new ChangeSkinRequest { NewSkin = newSkin };
        var data = await _client.ChangeSkinAsync(request, cancellationToken: cancellationToken);
        return new(Guid.Parse(data.ID), data.Name, data.Email, (byte)data.Skin, data.Position.X, data.Position.Y);
    }
}
