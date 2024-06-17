using System.Threading;
using System.Threading.Tasks;
using GameCore.Protos;

namespace GameClient;

sealed partial class GameApiClient
{
    public async ValueTask MoveAsync(float x, float y, CancellationToken cancellationToken = default)
    {
        var request = new MoveRequest { Position = new Vector2 { X = x, Y = y } };
        await _client.MoveAsync(request, cancellationToken: cancellationToken);
    }
}
