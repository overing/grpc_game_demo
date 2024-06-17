using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GameClient;
using GameCore.Models;
using GameCore.Protos;
using Grpc.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainScreen : MonoBehaviour
{
    IGameApiClient _client;
    ServerTime _serverTime;
    Player _player;

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
        _chatInputField.onSubmit.AddListener(OnChatInputSubmit);

        _ = SyncCharactersAsync();
        _ = SyncChstAsync();

        EnqueueChat("/name NAME: to change name");
        EnqueueChat("/skin (1|2): to change skin");
    }

    void OnChatButtonClick() => OnChatInputSubmit(_chatInputField.text);

    static readonly Regex ChangeNameCommandPattern = new(@"/name (?<name>[\w|-|\.]+)");

    static readonly Regex ChangeSkinCommandPattern = new(@"/skin (?<skin>[1|2])");

    void OnChatInputSubmit(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return;

        _chatInputField.text = string.Empty;
        if (input.Equals("/echo", StringComparison.InvariantCultureIgnoreCase))
        {
            _ = EchoAsync();
            return;
        }

        if (input.StartsWith("/name", StringComparison.InvariantCultureIgnoreCase))
        {
            if (ChangeNameCommandPattern.Match(input) is Match match && match.Groups["name"].Value is string newName)
                _ = ChangeNameAsync(newName);
            else
                EnqueueChat("Command format error");
            return;
        }

        if (input.StartsWith("/skin", StringComparison.InvariantCultureIgnoreCase))
        {
            if (ChangeSkinCommandPattern.Match(input) is Match match && match.Groups["skin"].Value is string newSkin)
                _ = ChangeSkinAsync(byte.Parse(newSkin));
            else
                EnqueueChat("Command format error");
            return;
        }

        if (input.Equals("/time", StringComparison.InvariantCultureIgnoreCase))
        {
            EnqueueChat(_serverTime.Now.ToString("F"));
            return;
        }

        _client.ChatAsync(input);
    }

    void EnqueueChat(string message, string sender = "<system>")
    {
        var chat = Instantiate(_templateChatText, _templateChatText.transform.parent);
        chat.name = "chat";
        chat.text = $"{sender}: {message}";
        chat.gameObject.SetActive(true);
        while (_chatContent.childCount > 65)
            Destroy(_chatContent.GetChild(0).gameObject);
    }

    async UniTask EchoAsync()
    {
        try
        {
            var begin = DateTimeOffset.Now;
            var data = await _client.EchoAsync(begin, destroyCancellationToken);

            var c2g = data.ClientToGateway.TotalMilliseconds;
            var g2s = data.GatewayToSilo.TotalMilliseconds;
            var s2g = data.SiloToGateway.TotalMilliseconds;
            var total = (DateTimeOffset.Now - begin).TotalMilliseconds;
            EnqueueChat($"c2g: {s2g:N0}ms, g2s: {g2s:N0}ms, s2g: {s2g:N0}ms, total: {total:N0}ms");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
        {
            _ = FaultScreen.CreateAsync(
                message: "Connection timeout, need login again, will back to title screen.",
                onClick: c => SceneManager.LoadScene("TitleScene"));
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException)
                return;
            EnqueueChat("Error: " + ex.ToString());
            Debug.LogException(ex);
        }
    }

    async UniTask ChangeNameAsync(string newName)
    {
        try
        {
            var data = await _client.ChangeNameAsync(newName, destroyCancellationToken);
            _player.Load(data);
            _nameText.text = data.Name;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
        {
            _ = FaultScreen.CreateAsync(
                message: "Connection timeout, need login again, will back to title screen.",
                onClick: c => SceneManager.LoadScene("TitleScene"));
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException)
                return;
            EnqueueChat("Error: " + ex.ToString());
            Debug.LogException(ex);
        }
    }

    async UniTask ChangeSkinAsync(byte newSkin)
    {
        try
        {
            await _client.ChangeSkinAsync(newSkin, destroyCancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
        {
            _ = FaultScreen.CreateAsync(
                message: "Connection timeout, need login again, will back to title screen.",
                onClick: c => SceneManager.LoadScene("TitleScene"));
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException)
                return;
            EnqueueChat("Error: " + ex.ToString());
            Debug.LogException(ex);
        }
    }

    async UniTask SyncCharactersAsync()
    {
        var duplex = _client.Client.SyncCharacters(cancellationToken: destroyCancellationToken);
        try
        {
            await duplex.ContinueAsync(new() { ID = _player.ID.ToString("N") }, TimeSpan.FromSeconds(30), HandleCharacterSync, destroyCancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Cancelled)
        {
            Debug.LogWarning("sync character connection canceled");
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable)
        {
            Debug.LogWarning("sync character connection unavailable");
        }
        catch (RpcException ex)
        {
            EnqueueChat("Error: " + ex.Message);
            Debug.LogException(ex);
        }
    }

    void HandleCharacterSync(SyncCharactersResponse response)
    {
        switch (response.ActionCase)
        {
            case SyncCharactersResponse.ActionOneofCase.Join:
                var addId = response.ID;
                if (FindObjectsOfType<CharacterController2D>().FirstOrDefault(c => c.name == addId) is CharacterController2D forExists)
                    Destroy(forExists.gameObject);
                var prefab = LoadCharacterGameObject((byte)response.Join.Skin);
                var instance = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
                if (addId == _player.ID.ToString("N"))
                    instance.AddComponent<SendMoveToClickPoint>();
                instance.name = addId;
                instance.transform.position = new(response.Join.Position.X, response.Join.Position.Y);
                instance.GetComponentInChildren<TextMesh>().text = CharacterData.CutNameForDisplay(response.Join.Name);
                break;

            case SyncCharactersResponse.ActionOneofCase.Leave:
                var deleteId = response.ID;
                if (FindObjectsOfType<CharacterController2D>().FirstOrDefault(c => c.name == deleteId) is CharacterController2D forDelete)
                    Destroy(forDelete.gameObject);
                else
                    Debug.LogWarningFormat("Delete id#{0} not found", deleteId);
                break;

            case SyncCharactersResponse.ActionOneofCase.Move:
                var moveId = response.ID;
                if (FindObjectsOfType<CharacterController2D>().FirstOrDefault(c => c.name == moveId) is CharacterController2D forMove)
                    forMove.SmoothMoveTo(new(response.Move.X, response.Move.Y));
                else
                    Debug.LogWarningFormat("Move id#{0} not found", moveId);
                break;

            case SyncCharactersResponse.ActionOneofCase.Rename:
                var changeNameId = response.ID;
                if (FindObjectsOfType<CharacterController2D>().FirstOrDefault(c => c.name == changeNameId) is CharacterController2D forChangeName)
                    forChangeName.SetName(CharacterData.CutNameForDisplay(response.Rename));
                else
                    Debug.LogWarningFormat("ChangeName id#{0} not found", changeNameId);
                break;

            case SyncCharactersResponse.ActionOneofCase.Skin:
                var skinId = response.ID;
                var originalPos = Vector3.zero;
                var originalName = string.Empty;
                if (FindObjectsOfType<CharacterController2D>().FirstOrDefault(c => c.name == skinId) is CharacterController2D forSkin)
                {
                    originalPos = forSkin.transform.position;
                    originalName = forSkin.GetComponentInChildren<TextMesh>().text;
                    Destroy(forSkin.gameObject);
                }
                else
                    break;
                var newPrefab = LoadCharacterGameObject((byte)response.Skin);
                var newInstance = Instantiate(newPrefab, originalPos, Quaternion.identity, transform);
                if (skinId == _player.ID.ToString("N"))
                    newInstance.AddComponent<SendMoveToClickPoint>();
                newInstance.name = skinId;
                newInstance.GetComponent<CharacterController2D>().SetName(CharacterData.CutNameForDisplay(originalName));
                break;
        }
    }

    static GameObject LoadCharacterGameObject(byte skin)
    {
        var path = $"Prefabs/Character{skin:0#}/Character";
        return LoadResourcesGameObject(path);
    }

    static GameObject LoadResourcesGameObject(string path)
    {
        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
            throw new System.IO.FileNotFoundException("Resources not found", path);
        return prefab;
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
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable)
        {
            Debug.LogWarning("sync character connection unavailable");
        }
        catch (RpcException ex)
        {
            EnqueueChat("Error: " + ex.Message);
            Debug.LogException(ex);
        }
    }

    async UniTask LogoutAsync()
    {
        try
        {
            await _client.LogoutAsync(destroyCancellationToken);

            await SceneManager.LoadSceneAsync("TitleScene");
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException)
                return;
            Debug.LogWarning(ex.Message);
        }
    }

    void OnApplicationQuit()
    {
        _ = LogoutAsync();
    }
}
