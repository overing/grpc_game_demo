using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GameClient;
using Grpc.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TitleScreen : MonoBehaviour
{
    string _userId;

    [SerializeField]
    Button _startButton;

    [SerializeField]
    Text _clientIdText;

    [SerializeField]
    Text _errorText;

    void Start()
    {
        if (PlayerPrefs.GetString("user_id", null) is not string userId || string.IsNullOrWhiteSpace(userId))
            PlayerPrefs.SetString("user_id", _userId = userId = Guid.NewGuid().ToString("N"));
        else
            _userId = userId;

        _errorText.text = string.Empty;
        _clientIdText.text = _userId;
        _startButton.onClick.AddListener(OnClickPanel);
    }

    void OnClickPanel()
    {
        _ = LoginAsync();
    }

    async UniTask LoginAsync()
    {
        try
        {
            _startButton.interactable = false;
            _startButton.GetComponentInChildren<Text>().text = "CONNECTION ...";
            _errorText.text = string.Empty;
            var client = Service.GetRequiredService<IGameApiClient>();
            var data = await client.LoginAsync(_userId, destroyCancellationToken);

            var serverTime = Service.GetRequiredService<ServerTime>();
            serverTime.Load(data.ServerTime);

            var player = Service.GetRequiredService<Player>();
            player.Load(data.User);

            await SceneManager.LoadSceneAsync("MainScene");
        }
        catch (TaskCanceledException)
        {
            _startButton.GetComponentInChildren<Text>().text = "START";
            _startButton.interactable = true;
        }
        catch (Exception ex)
        {
            _errorText.text = ex is RpcException rpcEx ? $"Rpc Error: {rpcEx.Status.DebugException.Message}" : $"Error: {ex.Message}";
            Debug.LogException(ex);
            _startButton.GetComponentInChildren<Text>().text = "START";
            _startButton.interactable = true;
        }
    }
}
