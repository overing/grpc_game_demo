using System;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

public static class Service
{
    static readonly string ServerAddress = "http://localhost:5000";
    static IServiceProvider _serviceProvider;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    public static void InitUniTaskLoop()
    {
        var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
        Cysharp.Threading.Tasks.PlayerLoopHelper.Initialize(ref loop);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeSynchronizationContext()
    {
        if (!Application.isEditor && Application.platform == RuntimePlatform.WebGLPlayer)
        {
            System.Threading.SynchronizationContext.SetSynchronizationContext(null); // for WebGL Grpc working
            OverrideFatchCookieRewrite(ServerAddress);
        }

        _serviceProvider = new ServiceCollection()
            .AddTransient<ChannelBase>(_ => GrpcChannel.ForAddress(ServerAddress, new()
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

    public static TService GetService<TService>() => _serviceProvider.GetService<TService>();

    public static TService GetRequiredService<TService>() => _serviceProvider.GetRequiredService<TService>();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void OverrideFatchCookieRewrite(string urlPrefix);
}
