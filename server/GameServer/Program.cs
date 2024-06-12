using GameServer.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Orleans.Configuration;
using Orleans.Serialization;
using OrleansDashboard;

var appBuilder = WebApplication.CreateBuilder(args);

appBuilder.Services.AddGameRepository();

var clusterOptionsConfig = appBuilder.Configuration.GetSection(nameof(ClusterOptions));
if (clusterOptionsConfig.Exists())
{
    appBuilder.Services.AddOrleans(siloBuilder =>
    {
        siloBuilder.Services.Configure<ClusterOptions>(clusterOptionsConfig);
        siloBuilder.Services.AddSerializer(serializerBuilder =>
        {
            serializerBuilder.AddJsonSerializer(isSupported: type => type.Namespace!.StartsWith("GameCore.Models"));
        });
        siloBuilder.UseRedisClustering(appBuilder.Configuration.GetConnectionString("cache"));

        var dashboardOptionsConfig = clusterOptionsConfig.GetSection(nameof(DashboardOptions));
        if (dashboardOptionsConfig.Exists())
            siloBuilder.UseDashboard(dashboardOptionsConfig.Bind);
    });
}

appBuilder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.EventsType = typeof(CustomCookieAuthenticationEvents);
    }).Services
    .AddScoped<CustomCookieAuthenticationEvents>();

var grpcConnectionString = appBuilder.Configuration.GetConnectionString("grpc");
if (grpcConnectionString is not null)
{
    appBuilder.Services.AddGrpc(options =>
    {
        options.EnableDetailedErrors = true;
    });

    appBuilder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policyBuilder =>
        {
            policyBuilder
                .SetIsOriginAllowed(origin =>
                {
                    var host = new Uri(origin).Host;
                    var result = host == "localhost" || host[..8] is "192.168.";
                    return result;
                });

            policyBuilder
                .AllowAnyMethod()
                .AllowCredentials()
                .AllowAnyHeader()
                .WithExposedHeaders("grpc-status", "grpc-message");
        });
    });
}

var app = appBuilder.Build();

if (grpcConnectionString is not null)
{
    app.UseCors();
    app.UseWebSockets();

    app.UseGrpcWebSocketRequestRoutingEnabler();

    app.UseRouting();

    app.UseAuthentication();

    app.UseGrpcWebSocketBridge();

    app.MapGrpcService<GameServer.GrpcServices.GameService>();
}
app.MapGet("/", () => "Is working now ( ^_^;)a");

app.Run(grpcConnectionString);
