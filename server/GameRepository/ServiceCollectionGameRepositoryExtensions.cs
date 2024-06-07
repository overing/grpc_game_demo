
using GameRepository;
using GameRepository.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionGameRepositoryExtensions
{
    public static IServiceCollection AddGameRepository(this IServiceCollection collection)
    {
            
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
