using System;
using System.Threading.Tasks;
using GameClient;
using GameCore.Models;
using Grpc.Core;
using UnityEngine;

public sealed class MainScreen : MonoBehaviour
{
    ServerTime _serverTime;
    Player _player;

    Task _task;
    string _echoResult;

    void Start()
    {
        _serverTime = Service.GetRequiredService<ServerTime>();
        _player = Service.GetRequiredService<Player>();
    }

    void OnGUI()
    {
        bool clickEcho;
        bool clickLogout;
        using (new GUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(Screen.width), GUILayout.Height(Screen.height)))
        {
            GUILayout.FlexibleSpace();

            using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label("ServerTime:", GUILayout.Width(GUI.skin.label.fontSize * 6));
                GUILayout.TextField(_serverTime.Now.ToString(), GUILayout.ExpandWidth(true));
            }
            using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label("Name:", GUILayout.Width(GUI.skin.label.fontSize * 5));
                GUILayout.TextField(_player.Name, GUILayout.ExpandWidth(true));
            }

            using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                GUI.enabled = _task is null;
                clickEcho = GUILayout.Button("ECHO", GUILayout.ExpandWidth(false));
                clickLogout = GUILayout.Button("LOGOUT", GUILayout.ExpandWidth(false));
                GUI.enabled = true;
            }

            if (_echoResult is { } result)
            {
                using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    GUILayout.Label("Result:", GUILayout.ExpandWidth(false));
                    GUILayout.TextArea(result, GUILayout.ExpandWidth(true));
                }
            }

            GUILayout.FlexibleSpace();
        }
        if (clickEcho)
            _ = EchoAsync();
        if (clickLogout)
            _ = LogoutAsync();
    }

    async ValueTask EchoAsync()
    {
        try
        {
            _echoResult = null;
            var client = Service.GetRequiredService<IGameApiClient>();
            var task = client.EchoAsync(DateTimeOffset.Now, destroyCancellationToken).AsTask();
            _task = task;
            var data = await task;

            _echoResult = data.ToString();
            _task = null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
        {
            var screen = new GameObject(nameof(FaultScreen)).AddComponent<FaultScreen>();
            screen.ErrorMessage = "Connection timeout, need login again, will back to title screen.";
            screen.OkClicked += c =>
            {
                new GameObject(nameof(TitleScreen), typeof(TitleScreen));
                Destroy(c.gameObject);
            };
            Destroy(gameObject);
        }
        catch (TaskCanceledException)
        {
            _task = null;
        }
        catch (Exception ex)
        {
            _echoResult = "Error: " + ex.ToString();
            Debug.LogException(ex);
            _task = null;
        }
    }

    async ValueTask LogoutAsync()
    {
        try
        {
            var client = Service.GetRequiredService<IGameApiClient>();
            _task = client.LogoutAsync(destroyCancellationToken).AsTask();
            await _task;

            _task = null;

            new GameObject(nameof(TitleScreen), typeof(TitleScreen));
            Destroy(gameObject);
        }
        catch (TaskCanceledException)
        {
            _task = null;
        }
        catch (Exception ex)
        {
            _echoResult = "Error: " + ex.ToString();
            Debug.LogException(ex);
            _task = null;
        }
    }
}
