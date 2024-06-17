using Cysharp.Threading.Tasks;
using GameClient;
using Grpc.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public sealed class SendMoveToClickPoint : MonoBehaviour
{
    IGameApiClient _client;

    void Awake() => _client = Service.GetRequiredService<IGameApiClient>();

    void LateUpdate()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject())
                return;

            var mousePos = Input.mousePosition;
            mousePos = new(mousePos.x, mousePos.y, Camera.main.nearClipPlane);
            var targetPos = (Vector2)Camera.main.ScreenToWorldPoint(mousePos);
            _ = SendMoveAsync(targetPos);
        }
    }

    async UniTask SendMoveAsync(Vector2 targetPos)
    {
        try
        {
            await _client.MoveAsync(targetPos.x, targetPos.y, destroyCancellationToken);
        }
        catch (RpcException rpcException)
        {
            if (rpcException.StatusCode == StatusCode.Unauthenticated)
                _ = FaultScreen.CreateAsync(
                    message: "Connection timeout, need login again, will back to title screen.",
                    onClick: c => SceneManager.LoadScene("TitleScene"));
        }
    }
}
