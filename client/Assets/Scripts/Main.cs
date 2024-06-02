using System;
using System.Threading.Tasks;
using GameCore.Protocols;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

public sealed class Main : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterSceneLoad() => DontDestroyOnLoad(new GameObject(nameof(Main), typeof(Main)));

    bool _guiInitialized;
    string _clientId;
    IServiceProvider _serviceProvider;
    IGameApiClient _gameApiClient;

    Task<LoginResponse> _loginTask;
    bool _tostErr;

    void Awake()
    {
        _serviceProvider = new ServiceCollection()
            .AddSingleton<IGameApiClient, GameApiClient>()
            .BuildServiceProvider();
    }

    void Start()
    {
        if (PlayerPrefs.GetString("client_id", null) is not string clientId || string.IsNullOrWhiteSpace(clientId))
            PlayerPrefs.SetString("client_id", _clientId = clientId = Guid.NewGuid().ToString("N"));
        else
            _clientId = clientId;

        _gameApiClient = _serviceProvider.GetRequiredService<IGameApiClient>();
    }

    void OnGUI()
    {
        var (width, height) = (Screen.width, Screen.height);
        if (!_guiInitialized)
        {
            GUI.skin.label.fontSize = height / 18;
            GUI.skin.textField.fontSize = height / 20;
            GUI.skin.button.fontSize = height / 20;
            _guiInitialized = true;
        }

        using (new GUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(width), GUILayout.Height(height)))
        {
            using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label("Client ID:", GUILayout.ExpandWidth(false));
                GUI.enabled = false;
                GUILayout.TextField(_clientId, GUILayout.ExpandWidth(true));
                GUI.enabled = true;
            }
            if (_loginTask is null)
            {
                if (GUILayout.Button("Login", GUILayout.ExpandWidth(true)))
                    _loginTask = _gameApiClient.LoginAsync(new() { UserId = _clientId }).AsTask();
            }
            else if (!_loginTask.IsCompleted)
            {
                GUI.enabled = false;
                GUILayout.Button("Login", GUILayout.ExpandWidth(true));
                GUI.enabled = true;
            }
            else if (_loginTask.IsFaulted)
            {
                using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    GUILayout.Label("Error:", GUILayout.Width(GUI.skin.label.fontSize * 3));
                    var exception = _loginTask.Exception.Flatten();
                    GUILayout.TextField(exception.Message, GUILayout.ExpandWidth(true));
                    if (!_tostErr)
                    {
                        Debug.LogException(exception);
                        _tostErr = true;
                    }
                }
            }
        }
    }

    void OnDestroy()
    {
        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();
    }
}
