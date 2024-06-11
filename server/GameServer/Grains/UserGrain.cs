using GameCore.Models;
using GameRepository.Repositories;

namespace GameServer.Grains;

[Alias("GameServer.Grains.IUserGrain")]
public interface IUserGrain : IGrainWithStringKey
{
    [Alias("EchoAsync")]
    ValueTask<EchoData> EchoAsync(DateTimeOffset clientTime, DateTimeOffset gatewayTime, GrainCancellationToken cancellationToken);

    [Alias("GetDataAsync")]
    ValueTask<UserData> GetDataAsync(GrainCancellationToken cancellationToken);

    [Alias("SetMapCodeAsync")]
    ValueTask SetMapCodeAsync(int mapCode, GrainCancellationToken cancellationToken);

    [Alias("GetMapCodeAsync")]
    ValueTask<int> GetMapCodeAsync(GrainCancellationToken cancellationToken);
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

    int _mapCode;

    public ValueTask SetMapCodeAsync(int mapCode, GrainCancellationToken cancellationToken)
    {
        _mapCode = mapCode;
        return ValueTask.CompletedTask;
    }

    public ValueTask<int> GetMapCodeAsync(GrainCancellationToken cancellationToken)
    {
        return ValueTask.FromResult(_mapCode);
    }
}
