using System.Threading;
using System.Threading.Tasks;
using GameCore.Models;

namespace GameClient
{
    public interface IGameApiClient
    {
        ValueTask<PlayerData> LoginAsync(string userId, CancellationToken cancellationToken = default);
    }
}
