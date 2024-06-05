using System;
using System.Threading.Tasks;
using GameCore.Protos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    Exception _loginException;

    void Awake()
    {
        _serviceProvider = new ServiceCollection()
            .AddTransient<IGameApiClient, GameApiClient>()
            .Configure<GameApiClientOptions>(options =>
            {
                options.Address = "http://localhost:5000";
            })
            .BuildServiceProvider();
    }

    void Start()
    {
        if (PlayerPrefs.GetString("client_id", null) is not string clientId || string.IsNullOrWhiteSpace(clientId))
            PlayerPrefs.SetString("client_id", _clientId = clientId = Guid.NewGuid().ToString("N"));
        else
            _clientId = clientId;
    }

    void OnGUI()
    {
        var (width, height) = (Screen.width, Screen.height);
        if (!_guiInitialized)
        {
            GUI.skin = Resources.Load<GUISkin>("GUISkin");
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
                using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    GUILayout.Label("Address:", GUILayout.Width(GUI.skin.label.fontSize * 4));
                    var options = _serviceProvider.GetRequiredService<IOptions<GameApiClientOptions>>();
                    options.Value.Address = GUILayout.TextField(options.Value.Address);
                }

                if (GUILayout.Button("Login", GUILayout.ExpandWidth(true)))
                {
                    _loginException = null;
                    _gameApiClient = _serviceProvider.GetRequiredService<IGameApiClient>();
                    _loginTask = _gameApiClient.LoginAsync(new() { UserId = _clientId }).AsTask();
                }
            }
            else if (!_loginTask.IsCompleted)
            {
                GUI.enabled = false;
                GUILayout.Button("Login", GUILayout.ExpandWidth(true));
                GUI.enabled = true;
            }
            else if (_loginTask.IsFaulted)
            {
                _loginException = _loginTask.Exception.Flatten();
                _loginTask = null;
            }
            else if (_loginTask.IsCanceled)
            {
                _loginTask = null;
            }
            else
            {
                using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    GUILayout.Label("Response:", GUILayout.Width(GUI.skin.label.fontSize * 5));
                    var response = _loginTask.Result;
                    GUILayout.TextField(response.Message, GUILayout.ExpandWidth(true));
                }
                using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    GUILayout.Label("ServerTime:", GUILayout.Width(GUI.skin.label.fontSize * 6));
                    var response = _loginTask.Result;
                    GUILayout.TextField(response.ServerTime.ToString(), GUILayout.ExpandWidth(true));
                }
            }

            if (_loginException is { } exception)
            {
                using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    GUILayout.Label("Error:", GUILayout.Width(GUI.skin.label.fontSize * 3));
                    GUILayout.TextField(exception.Message, GUILayout.ExpandWidth(true));
                }
            }
        }
    }

    void OnDestroy()
    {
        if (_gameApiClient is IDisposable client)
        {
            client.Dispose();
            _gameApiClient = null;
        }

        if (_serviceProvider is IDisposable provider)
        {
            provider.Dispose();
            _serviceProvider = null;
        }
    }
}
