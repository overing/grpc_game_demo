using GameCore.Models;
using GameRepository;

namespace GameServer.Grains;

[Alias("GameServer.Grains.IUserGrain")]
public interface IUserGrain : IGrainWithStringKey
{
    [Alias("LoginAsync")]
    ValueTask<LoginData> LoginAsync(GrainCancellationToken cancellationToken);
}

sealed class UserGrain(
    ILogger<UserGrain> logger,
    IUserRepository userRepository) : Grain, IUserGrain
{
    public async ValueTask<LoginData> LoginAsync(GrainCancellationToken cancellationToken)
    {
        logger.LogTrace(nameof(LoginAsync));

        var account = this.GetPrimaryKeyString();
        var user = await userRepository.GetWithAccountAsync(account, cancellationToken.CancellationToken);
        user ??= await userRepository.CreateAsync(account, "Guest-" + account, "none@none", cancellationToken.CancellationToken);

        var serverTime = DateTimeOffset.Now;

        var userData = new UserData(
            ID: user.ID,
            Name: user.Name,
            Email: user.Email);

        var data = new LoginData(
            ServerTime: serverTime,
            User: userData);

        return data;
    }
}
