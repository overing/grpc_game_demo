using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GameClient;
using GameCore.Models;
using Grpc.Core;
using UnityEngine;

public sealed class MainScreen : MonoBehaviour
{
    IGameApiClient _client;
    ServerTime _serverTime;
    Player _player;

    Task _task;

    readonly Queue<ChatData> _chats = new();
    Vector2 _messageScrollPosition;
    string _input;

    void Start()
    {
        _client = Service.GetRequiredService<IGameApiClient>();
        _serverTime = Service.GetRequiredService<ServerTime>();
        _player = Service.GetRequiredService<Player>();

        _ = SyncCharactersAsync();
        _ = SyncChstAsync();
    }

    void OnGUI()
    {
        bool clickLogout;
        bool clickSend;
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
                clickLogout = GUILayout.Button("LOGOUT", GUILayout.ExpandWidth(false));
            }

            GUILayout.FlexibleSpace();

            using (var scroll = new GUILayout.ScrollViewScope(_messageScrollPosition, GUI.skin.box, GUILayout.ExpandWidth(true)))
            {
                _messageScrollPosition = scroll.scrollPosition;
                foreach (var chat in _chats)
                {
                    using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                    {
                        GUILayout.Label(chat.Sender, GUILayout.ExpandWidth(false));
                        GUILayout.Label(chat.Message, GUILayout.ExpandWidth(true));
                    }
                }
            }

            using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                GUI.enabled = _task is null;
                _input = GUILayout.TextField(_input, GUILayout.ExpandWidth(true));
                clickSend = GUILayout.Button("SEND", GUILayout.ExpandWidth(false));
                GUI.enabled = true;
            }
        }
        if (clickLogout)
            _ = LogoutAsync();
        if (clickSend)
            HandleSend();
    }

    void EnqueueSystemMessage(string message, string sender = "<system>")
    {
        _chats.Enqueue(new(sender, message));
        while (_chats.Count > 64)
            _chats.Dequeue();
        _messageScrollPosition.y = float.MaxValue;
    }

    void HandleSend()
    {
        if (string.IsNullOrWhiteSpace(_input))
            return;

        if (_input.Equals("/echo", StringComparison.InvariantCultureIgnoreCase))
        {
            _ = EchoAsync();
            _input = string.Empty;
            return;
        }

        _client.ChatAsync(_input);
        _input = string.Empty;
    }

    async ValueTask EchoAsync()
    {
        try
        {
            var task = _client.EchoAsync(DateTimeOffset.Now, destroyCancellationToken).AsTask();
            _task = task;
            var data = await task;

            EnqueueSystemMessage(data.ToString());
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
            EnqueueSystemMessage("Error: " + ex.ToString());
            Debug.LogException(ex);
            _task = null;
        }
    }

    UniTask SyncCharactersAsync()
    {
        var duplex = _client.Client.SyncCharacters(cancellationToken: destroyCancellationToken);
        return duplex.ContinueAsync(new() { ID = _player.ID.ToString("N") }, TimeSpan.FromSeconds(30), response =>
        {
            var character = response.Character;
            switch ((SyncCharacterAction)response.Action)
            {
                case SyncCharacterAction.Add:
                    var addId = character.ID;
                    if (FindObjectsOfType<CharacterController2D>().FirstOrDefault(c => c.name == addId) is CharacterController2D forExists)
                        Destroy(forExists.gameObject);
                    var prefab = Resources.Load<GameObject>("Prefabs/Character");
                    var instance = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
                    if (character.ID == _player.ID.ToString("N"))
                        instance.AddComponent<SendMoveToClickPoint>();
                    instance.name = addId;
                    instance.transform.position = new Vector2(character.Position.X, character.Position.Y);
                    break;

                case SyncCharacterAction.Move:
                    var moveId = character.ID;
                    if (FindObjectsOfType<CharacterController2D>().FirstOrDefault(c => c.name == moveId) is CharacterController2D forMove)
                        forMove.SmoothMoveTo(new Vector2(character.Position.X, character.Position.Y));
                    else
                        Debug.LogWarningFormat("Move id#{0} not found", moveId);
                    break;

                case SyncCharacterAction.Delete:
                    var deleteId = character.ID;
                    if (FindObjectsOfType<CharacterController2D>().FirstOrDefault(c => c.name == deleteId) is CharacterController2D forDelete)
                        Destroy(forDelete.gameObject);
                    else
                        Debug.LogWarningFormat("Delete id#{0} not found", deleteId);
                    break;
            }
        }, destroyCancellationToken);
    }

    UniTask SyncChstAsync()
    {
        var duplex = _client.Client.SyncChat(cancellationToken: destroyCancellationToken);
        return duplex.ContinueAsync(new() { ID = _player.ID.ToString("N") }, TimeSpan.FromSeconds(30), response =>
        {
            var data = new ChatData(response.Sender, response.Message);
            _chats.Enqueue(data);
        }, destroyCancellationToken);
    }

    async ValueTask LogoutAsync()
    {
        try
        {
            _task = _client.LogoutAsync(destroyCancellationToken).AsTask();
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
            EnqueueSystemMessage("Error: " + ex.ToString());
            Debug.LogException(ex);
            _task = null;
        }
    }
}
