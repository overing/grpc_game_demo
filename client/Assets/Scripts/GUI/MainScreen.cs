using System;
using System.Linq;
using System.Threading;
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
    bool _hiddenJoin;

    void Start()
    {
        _serverTime = Service.GetRequiredService<ServerTime>();
        _player = Service.GetRequiredService<Player>();
    }

    async ValueTask SyncCharactersAsync(CancellationToken cancellationToken)
    {
        var player = Service.GetRequiredService<Player>();
        var client = Service.GetRequiredService<IGameApiClient>();
        var stream = client.SyncCharactersAsync(player.ID, cancellationToken);
        try
        {
            _hiddenJoin = true;
            await foreach (var data in stream.WithCancellation(cancellationToken))
            {
                switch (data.Action)
                {
                    case SyncCharacterAction.Add:
                        var addId = data.Character.ID.ToString("N");
                        if (FindObjectsOfType<CharacterController2D>().FirstOrDefault(c => c.name == addId) is CharacterController2D forExists)
                            Destroy(forExists.gameObject);
                        var prefab = Resources.Load<GameObject>("Prefabs/Character");
                        var instance = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
                        if (data.Character.ID == player.ID)
                            instance.AddComponent<SendMoveToClickPoint>();
                        instance.name = addId;
                        instance.transform.position = new Vector2(data.Character.Position.X, data.Character.Position.Y);
                        break;

                    case SyncCharacterAction.Move:
                        var moveId = data.Character.ID.ToString("N");
                        if (FindObjectsOfType<CharacterController2D>().FirstOrDefault(c => c.name == moveId) is CharacterController2D forMove)
                            forMove.SmoothMoveTo(new Vector2(data.Character.Position.X, data.Character.Position.Y));
                        else
                            Debug.LogWarningFormat("Move id#{0} not found", moveId);
                        break;

                    case SyncCharacterAction.Delete:
                        var deleteId = data.Character.ID.ToString("N");
                        if (FindObjectsOfType<CharacterController2D>().FirstOrDefault(c => c.name == deleteId) is CharacterController2D forDelete)
                            Destroy(forDelete.gameObject);
                        else
                            Debug.LogWarningFormat("Delete id#{0} not found", deleteId);
                        break;

                }
            }
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            Debug.LogException(ex);
            var screen = new GameObject(nameof(FaultScreen)).AddComponent<FaultScreen>();
            screen.ErrorMessage = "Connection faulted, need login again, will back to title screen.";
            screen.OkClicked += c =>
            {
                new GameObject(nameof(TitleScreen), typeof(TitleScreen));
                Destroy(c.gameObject);
            };
            Destroy(gameObject);
        }
        Debug.LogWarning("SyncCharactersAsync end");
    }

    void OnGUI()
    {
        bool clickEcho;
        bool clickJoin = false;
        bool clickLogout;
        using (new GUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(Screen.width), GUILayout.Height(Screen.height)))
        {
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
                if (!_hiddenJoin)
                    clickJoin = GUILayout.Button("JOIN", GUILayout.ExpandWidth(false));
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
        }
        if (clickEcho)
            _ = EchoAsync();
        if (clickJoin)
            _ = SyncCharactersAsync(destroyCancellationToken);
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
