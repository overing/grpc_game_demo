using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameRepository;

public sealed class GameDbContext(
    ILogger<GameDbContext> logger,
    DbContextOptions<GameDbContext> options,
    IServiceProvider services)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyConfigurationsFromServiceProvider(modelBuilder);
    }

    void ApplyConfigurationsFromServiceProvider(ModelBuilder modelBuilder)
    {
        var method = typeof(ModelBuilder).GetMethods().Single(IsApplyEntityConfigurationMethod);
        var assembly = Assembly.GetExecutingAssembly();
        var foundOne = false;
        foreach (var @interface in assembly.GetTypes().OrderBy(t => t.FullName).SelectMany(t => t.GetInterfaces()))
        {
            if (!@interface.IsGenericType || @interface.GetGenericTypeDefinition() != typeof(IEntityTypeConfiguration<>))
                continue;

            var config = services.GetService(@interface);
            var target = method.MakeGenericMethod(@interface.GenericTypeArguments[0]);
            target.Invoke(modelBuilder, [config]);
            foundOne = true;
        }

        if (!foundOne)
            logger.LogWarning("NoEntityTypeConfigurationsWarning: {assembly}", assembly);

        static bool IsApplyEntityConfigurationMethod(MethodInfo method)
        {
            return method is { Name: nameof(ModelBuilder.ApplyConfiguration), ContainsGenericParameters: true }
                    && method.GetParameters().SingleOrDefault()?.ParameterType.GetGenericTypeDefinition()
                    == typeof(IEntityTypeConfiguration<>);
        }
    }
}
