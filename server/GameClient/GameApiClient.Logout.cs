using System.Threading;
using System.Threading.Tasks;

namespace GameClient;

sealed partial class GameApiClient
{
    public async ValueTask LogoutAsync(CancellationToken cancellationToken = default)
    {
        _ = await _client.LogoutAsync(new(), cancellationToken: cancellationToken);
    }
}
