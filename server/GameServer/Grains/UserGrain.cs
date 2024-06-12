using GameCore.Models;
using GameRepository.Repositories;

namespace GameServer.Grains;

[Alias("GameServer.Grains.IUserGrain")]
public interface IUserGrain : IGrainWithGuidKey
{
    [Alias("EchoAsync")]
    ValueTask<EchoData> EchoAsync(DateTimeOffset clientTime, DateTimeOffset gatewayTime, GrainCancellationToken cancellationToken);

    [Alias("GetDataAsync")]
    ValueTask<UserData> GetDataAsync(GrainCancellationToken cancellationToken);
}

sealed class UserGrain(
    ILogger<UserGrain> logger,
    IUserRepository userRepository,
    TimeProvider timeProvider)
    : Grain, IUserGrain
{
    public async ValueTask<UserData> GetDataAsync(GrainCancellationToken cancellationToken)
    {
        logger.LogTrace(nameof(GetDataAsync));

        var id = Guid.Parse(this.GetPrimaryKeyString());

        var user = await userRepository.GetWithIdAsync(id, cancellationToken.CancellationToken)
            ?? throw new Exception($"User not found of ID: {this.GetPrimaryKeyString()}");

        return new UserData(
            ID: id,
            Name: user.Name,
            Email: user.Email);
    }

    public ValueTask<EchoData> EchoAsync(DateTimeOffset clientTime, DateTimeOffset gatewayTime, GrainCancellationToken cancellationToken)
    {
        logger.LogTrace(nameof(EchoAsync));

        var serverTime = timeProvider.GetLocalNow();

        var data = new EchoData(
            ClientToGateway: gatewayTime - clientTime,
            GatewayToSilo: serverTime - gatewayTime,
            SiloToGateway: default,
            SiloTime: serverTime);

        return ValueTask.FromResult(data);
    }
}
