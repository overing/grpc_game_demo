using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GameClient;
using GameCore.Models;
using Grpc.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainScreen : MonoBehaviour
{
    IGameApiClient _client;
    ServerTime _serverTime;
    Player _player;

    Task _task;

    [SerializeField]
    Transform _chatContent;

    [SerializeField]
    Text _templateChatText;

    [SerializeField]
    InputField _chatInputField;

    [SerializeField]
    Button _chatButton;

    [SerializeField]
    Text _nameText;

    void Start()
    {
        _client = Service.GetRequiredService<IGameApiClient>();
        _serverTime = Service.GetRequiredService<ServerTime>();
        _player = Service.GetRequiredService<Player>();

        _nameText.text = _player.Name;

        _templateChatText.gameObject.SetActive(false);

        _chatButton.onClick.AddListener(OnChatButtonClick);

        _ = SyncCharactersAsync();
        _ = SyncChstAsync();
    }

    void OnChatButtonClick()
    {
        var input = _chatInputField.text;
        if (string.IsNullOrWhiteSpace(input))
            return;

        if (input.Equals("/echo", StringComparison.InvariantCultureIgnoreCase))
        {
            _ = EchoAsync();
            _chatInputField.text = string.Empty;
            return;
        }

        if (input.Equals("/time", StringComparison.InvariantCultureIgnoreCase))
        {
            EnqueueChat(_serverTime.Now.ToString("F"));
            _chatInputField.text = string.Empty;
            return;
        }

        _client.ChatAsync(input);
        _chatInputField.text = string.Empty;
    }

    void EnqueueChat(string message, string sender = "<system>")
    {
        var chat = Instantiate(_templateChatText, _templateChatText.transform.parent);
        chat.name = "chat";
        chat.text = $"{sender}: {message}";
        chat.gameObject.SetActive(true);
        while (_chatContent.childCount > 65)
            Destroy(_chatContent.GetChild(0).gameObject);
        // _messageScrollPosition.y = float.MaxValue;
    }

    async ValueTask EchoAsync()
    {
        try
        {
            var task = _client.EchoAsync(DateTimeOffset.Now, destroyCancellationToken).AsTask();
            _task = task;
            var data = await task;

            EnqueueChat(data.ToString());
            _task = null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
        {
            _ = FaultScreen.CreateAsync(
                message: "Connection timeout, need login again, will back to title screen.",
                onClick: c => SceneManager.LoadScene("TitleScene"));
        }
        catch (TaskCanceledException)
        {
            _task = null;
        }
        catch (Exception ex)
        {
            EnqueueChat("Error: " + ex.ToString());
            Debug.LogException(ex);
            _task = null;
        }
    }

    async UniTask SyncCharactersAsync()
    {
        var duplex = _client.Client.SyncCharacters(cancellationToken: destroyCancellationToken);
        try
        {
            await duplex.ContinueAsync(new() { ID = _player.ID.ToString("N") }, TimeSpan.FromSeconds(30), response =>
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
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            Debug.LogWarning("sync character connection canceled");
        }
        catch (RpcException ex)
        {
            EnqueueChat("Error: " + ex.Message);
            Debug.LogException(ex);
            _task = null;
        }
    }

    async UniTask SyncChstAsync()
    {
        var duplex = _client.Client.SyncChat(cancellationToken: destroyCancellationToken);
        try
        {
            await duplex.ContinueAsync(new() { ID = _player.ID.ToString("N") }, TimeSpan.FromSeconds(30), response =>
            {
                EnqueueChat(response.Message, response.Sender);
            }, destroyCancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            Debug.LogWarning("sync chat connection canceled");
        }
        catch (RpcException ex)
        {
            EnqueueChat("Error: " + ex.Message);
            Debug.LogException(ex);
            _task = null;
        }
    }

    void OnApplicationQuit()
    {
        _ = LogoutAsync();
    }

    async ValueTask LogoutAsync()
    {
        try
        {
            _task = _client.LogoutAsync(destroyCancellationToken).AsTask();
            await _task;

            _task = null;

            await SceneManager.LoadSceneAsync("TitleScene");
        }
        catch (TaskCanceledException)
        {
            _task = null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning(ex.Message);
            _task = null;
        }
    }
}
