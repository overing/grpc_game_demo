using Orleans.Serialization;

var appBuilder = WebApplication.CreateBuilder(args);

appBuilder.Services.AddGameRepository();

appBuilder.Services.AddOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    siloBuilder.UseDashboard();
    siloBuilder.Services.AddSerializer(serializerBuilder =>
    {
        serializerBuilder.AddJsonSerializer(isSupported: type => type.Namespace!.StartsWith("GameCore.Models"));
    });
});

appBuilder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
});
appBuilder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policyBuilder =>
    {
        policyBuilder.AllowAnyMethod();
        policyBuilder.AllowAnyOrigin();
        policyBuilder.AllowAnyHeader();

        policyBuilder.WithExposedHeaders("grpc-status", "grpc-message");
    });
});

var app = appBuilder.Build();

app.UseCors();
app.UseWebSockets();
app.UseGrpcWebSocketRequestRoutingEnabler();

app.UseRouting();

app.UseGrpcWebSocketBridge();

app.MapGrpcService<GameServer.GrpcServices.GameService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run("http://localhost:5000");
