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
    Exception _loginError;

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

        bool clickStart;
        using (new GUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(Screen.width), GUILayout.Height(Screen.height)))
        {
            GUILayout.FlexibleSpace();

            using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label("Client ID:", GUILayout.ExpandWidth(false));
                GUILayout.TextField(_userId, GUILayout.ExpandWidth(true));
            }

            using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                GUILayout.FlexibleSpace();

                GUI.enabled = _loginTask is null;
                clickStart = GUILayout.Button("START", GUILayout.ExpandWidth(false));
                GUI.enabled = true;

                GUILayout.FlexibleSpace();
            }

            if (_loginError is { } exception)
            {
                using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    GUILayout.Label("Error:", GUILayout.ExpandWidth(false));
                    GUILayout.TextArea(exception.Message, GUILayout.ExpandWidth(true));
                }
            }

            GUILayout.FlexibleSpace();
        }
        if (clickStart)
            _ = LoginAsync();
    }

    async ValueTask LoginAsync()
    {
        try
        {
            _loginError = null;
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
            _loginError = ex;
            Debug.LogException(ex);
            _loginTask = null;
        }
    }
}
