using System.Threading;
using System.Threading.Tasks;
using GameCore.Protos;

namespace GameClient;

sealed partial class GameApiClient
{
    public async ValueTask ChatAsync(string message, CancellationToken cancellationToken = default)
    {
        var request = new ChatRequest { Message = message };
        await _client.ChatAsync(request, cancellationToken: cancellationToken);
    }
}
