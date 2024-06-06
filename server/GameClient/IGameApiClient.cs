using System.Threading;
using System.Threading.Tasks;

namespace GameClient
{
    public interface IGameApiClient
    {
        ValueTask<LoginData> LoginAsync(string userId, CancellationToken cancellationToken = default);
    }
}
