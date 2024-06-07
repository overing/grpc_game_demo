using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace GameRepository;

public sealed class DatabaseStartup(IDbContextFactory<GameDbContext> contextFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
