using GameRepository;
using GameRepository.Models;
using GameRepository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis.Extensions.Core;
using StackExchange.Redis.Extensions.Core.Abstractions;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Core.Implementations;
using StackExchange.Redis.Extensions.System.Text.Json;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionGameRepositoryExtensions
{
    public static IServiceCollection AddGameRepository(this IServiceCollection collection)
    {
        collection.AddSingleton<IRedisClientFactory, RedisClientFactory>();
        collection.AddSingleton<ISerializer, SystemTextJsonSerializer>();
        collection.AddSingleton(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("cache");
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            return new RedisConfiguration { Name = "session-cache", ConnectionString = connectionString };
        });
        collection.AddSingleton<ISessionRepository, SessionRepository>();

        return collection
            .AddDbContextFactory<GameDbContext>((provider, options) =>
            {
                var configuration = provider.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString("db");
                var serverVersion = ServerVersion.AutoDetect(connectionString);
                options.UseMySql(connectionString, serverVersion);
            })
            .AddSingleton<IEntityTypeConfiguration<User>, UserConfig>()
            .AddHostedService<DatabaseStartup>()

            .AddTransient<IUserRepository, UserRepository>()
            .AddSingleton(TimeProvider.System);

    }
}
