using System;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

public static class Service
{
    static readonly string _serverAddress = "http://localhost:5000";
    static IServiceProvider _serviceProvider;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeSynchronizationContext()
    {
        if (!Application.isEditor && Application.platform == RuntimePlatform.WebGLPlayer)
            System.Threading.SynchronizationContext.SetSynchronizationContext(null); // for WebGL Grpc working

        _serviceProvider = new ServiceCollection()
            .AddTransient<ChannelBase>(_ => GrpcChannel.ForAddress(_serverAddress, new()
            {
                HttpHandler = new GrpcWebSocketBridge.Client.GrpcWebSocketBridgeHandler(),
                DisposeHttpClient = true,
            }))
            .AddGameClient(disposeChannel: true, lifetime: ServiceLifetime.Singleton)
            .AddSingleton<ServerTime>()
            .AddSingleton<Player>()
            .BuildServiceProvider();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange mode)
        {
            if (mode != UnityEditor.PlayModeStateChange.ExitingPlayMode)
                return;

            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
                _serviceProvider = null;
            }
        }
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterSceneLoad() => new GameObject(nameof(TitleScreen), typeof(TitleScreen));

    public static TService GetService<TService>() => _serviceProvider.GetService<TService>();

    public static TService GetRequiredService<TService>() => _serviceProvider.GetRequiredService<TService>();
}
