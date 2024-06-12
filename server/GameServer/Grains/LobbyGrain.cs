using GameCore.Models;
using GameRepository.Repositories;
using Orleans.Concurrency;

namespace GameServer.Grains;

[Alias("GameServer.Grains.ILobbyGrain")]
public interface ILobbyGrain : IGrainWithIntegerKey
{
    [Alias("LoginAsync")]
    ValueTask<LoginData> LoginAsync(string account, GrainCancellationToken cancellationToken);
}

[StatelessWorker]
sealed class LobbyGrain(
    ILogger<LobbyGrain> logger,
    IUserRepository userRepository,
    TimeProvider timeProvider)
    : Grain, ILobbyGrain
{
    public async ValueTask<LoginData> LoginAsync(string account, GrainCancellationToken cancellationToken)
    {
        logger.LogTrace(nameof(LoginAsync));

        ArgumentException.ThrowIfNullOrWhiteSpace(account);

        var serverTime = timeProvider.GetLocalNow();
        var user = await userRepository.GetWithAccountAsync(account, cancellationToken.CancellationToken);
        if (user is null)
            user = await userRepository.CreateAsync(account, "Guest-" + account, "none@none", cancellationToken.CancellationToken);
        else
            await userRepository.UpdateLoginTimeAsync(user.ID, serverTime);

        var data = new LoginData(
            ServerTime: serverTime,
            User: user);

        return data;
    }
}
