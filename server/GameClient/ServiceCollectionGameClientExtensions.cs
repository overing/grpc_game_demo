using GameClient;
using Grpc.Core;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionGameClientExtensions
{
    public static IServiceCollection AddGameClient(this IServiceCollection collection, bool disposeChannel, ServiceLifetime lifetime)
    {
        var descriptor = new ServiceDescriptor(typeof(IGameApiClient), provider =>
        {
            var channel = provider.GetRequiredService<ChannelBase>();
            return new GameApiClient(channel, disposeChannel);
        }, lifetime);
        collection.Add(descriptor);
        return collection;
    }
}
