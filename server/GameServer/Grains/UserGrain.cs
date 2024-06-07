using GameCore.Models;

namespace GameServer.Grains;

[Alias("GameServer.Grains.IUserGrain")]
public interface IUserGrain : IGrainWithStringKey
{
    [Alias("EchoAsync")]
    ValueTask<EchoData> EchoAsync(DateTimeOffset clientTime, DateTimeOffset gatewayTime, GrainCancellationToken cancellationToken);
}

sealed class UserGrain(
    ILogger<UserGrain> logger,
    TimeProvider timeProvider)
    : Grain, IUserGrain
{
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
