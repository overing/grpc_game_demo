using GameCore.Models;
using GameRepository.Repositories;

namespace GameServer.Grains;

[Alias("GameServer.Grains.IUserGrain")]
public interface IUserGrain : IGrainWithGuidKey
{
    [Alias("EchoAsync")]
    ValueTask<EchoData> EchoAsync(DateTimeOffset clientTime, DateTimeOffset gatewayTime, GrainCancellationToken cancellationToken);

    [Alias("SetPositionAsync")]
    ValueTask<UserData> SetPositionAsync(PointFloat position, GrainCancellationToken cancellationToken);

    [Alias("ChangeNameAsync")]
    ValueTask<UserData> ChangeNameAsync(string newName, GrainCancellationToken cancellationToken);

    [Alias("ChangeSkinAsync")]
    ValueTask<UserData> ChangeSkinAsync(byte newSkin, GrainCancellationToken cancellationToken);

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

        var userId = this.GetPrimaryKey();

        var user = await userRepository.GetWithIdAsync(userId, cancellationToken.CancellationToken)
            ?? throw new Exception($"User not found of ID: {userId:N}");

        return user;
    }

    public async ValueTask<UserData> SetPositionAsync(PointFloat position, GrainCancellationToken cancellationToken)
    {
        logger.LogTrace(nameof(SetPositionAsync));

        var userId = this.GetPrimaryKey();

        var data = await userRepository.UpdatePositionAsync(userId, position.X, position.Y)
            ?? throw new Exception($"User not found of ID: {userId:N}");

        return data;
    }

    public async ValueTask<UserData> ChangeNameAsync(string newName, GrainCancellationToken cancellationToken)
    {
        logger.LogTrace(nameof(ChangeNameAsync));

        var userId = this.GetPrimaryKey();

        var data = await userRepository.UpdateNameAsync(userId, newName)
            ?? throw new Exception($"User not found of ID: {userId:N}");

        return data;
    }

    public async ValueTask<UserData> ChangeSkinAsync(byte newSkin, GrainCancellationToken cancellationToken)
    {
        logger.LogTrace(nameof(ChangeSkinAsync));

        var userId = this.GetPrimaryKey();

        var data = await userRepository.UpdateSkinAsync(userId, newSkin)
            ?? throw new Exception($"User not found of ID: {userId:N}");

        return data;
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
