using GameServer.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Orleans.Configuration;
using Orleans.Serialization;

var appBuilder = WebApplication.CreateBuilder(args);

appBuilder.Services.AddGameRepository();

appBuilder.Services.AddOrleans(siloBuilder =>
{
    siloBuilder.Services.Configure<ClusterOptions>(appBuilder.Configuration.GetSection(nameof(ClusterOptions)));
    siloBuilder.UseRedisClustering(appBuilder.Configuration.GetConnectionString("cache"));
    siloBuilder.UseDashboard();
    siloBuilder.Services.AddSerializer(serializerBuilder =>
    {
        serializerBuilder.AddJsonSerializer(isSupported: type => type.Namespace!.StartsWith("GameCore.Models"));
    });
});

appBuilder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.EventsType = typeof(CustomCookieAuthenticationEvents);
    }).Services
    .AddScoped<CustomCookieAuthenticationEvents>();

appBuilder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
});

appBuilder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policyBuilder =>
    {
        policyBuilder
            .SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
            .AllowAnyMethod()
            .AllowCredentials()
            .AllowAnyHeader()
            .WithExposedHeaders("grpc-status", "grpc-message");
    });
});

var app = appBuilder.Build();

app.UseCors();
app.UseWebSockets();

app.UseGrpcWebSocketRequestRoutingEnabler();

app.UseRouting();

app.UseAuthentication();

app.UseGrpcWebSocketBridge();

app.MapGrpcService<GameServer.GrpcServices.GameService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run("http://localhost:5000");
