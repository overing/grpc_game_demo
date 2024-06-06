using System;
using System.Threading.Tasks;
using GameClient;
using GameCore.Models;
using UnityEngine;

public sealed class TitleScreen : MonoBehaviour
{
    string _userId;

    bool _guiInitialized;

    Task<PlayerData> _loginTask;

    void Start()
    {
        if (PlayerPrefs.GetString("user_id", null) is not string userId || string.IsNullOrWhiteSpace(userId))
            PlayerPrefs.SetString("user_id", _userId = userId = Guid.NewGuid().ToString("N"));
        else
            _userId = userId;
    }

    void OnGUI()
    {
        if (!_guiInitialized)
        {
            var height = Screen.height;
            GUI.skin = Resources.Load<GUISkin>("GUISkin");
            GUI.skin.label.fontSize = height / 18;
            GUI.skin.textField.fontSize = height / 20;
            GUI.skin.button.fontSize = height / 20;
            _guiInitialized = true;
        }

        using (new GUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(Screen.width), GUILayout.Height(Screen.height)))
        {
            using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label("Client ID:", GUILayout.ExpandWidth(false));
                GUI.enabled = false;
                GUILayout.TextField(_userId, GUILayout.ExpandWidth(true));
                GUI.enabled = true;
            }

            if (_loginTask is null)
            {
                if (GUILayout.Button("Login", GUILayout.ExpandWidth(true)))
                    _ = LoginAsync();
            }
            else if (!_loginTask.IsCompleted)
            {
                GUI.enabled = false;
                GUILayout.Button("Login", GUILayout.ExpandWidth(true));
                GUI.enabled = true;
            }
            else if (!_loginTask.IsCanceled && _loginTask.IsFaulted)
            {
                var exception = _loginTask.Exception.Flatten().InnerException;
                using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    GUILayout.Label("Error:", GUILayout.Width(GUI.skin.label.fontSize * 3));
                    GUILayout.TextField(exception.Message, GUILayout.ExpandWidth(true));
                }
            }
        }
    }

    async ValueTask LoginAsync()
    {
        try
        {
            var client = Service.GetRequiredService<IGameApiClient>();
            _loginTask = client.LoginAsync(_userId).AsTask();
            var data = await _loginTask;

            var player = Service.GetRequiredService<Player>();
            player.Load(data);

            new GameObject(nameof(MainScreen), typeof(MainScreen));
            Destroy(gameObject);
        }
        catch (TaskCanceledException)
        {
            _loginTask = null;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            _loginTask = null;
        }
    }
}
