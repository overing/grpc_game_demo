using System.Threading.Tasks;
using GameClient;
using Grpc.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public sealed class SendMoveToClickPoint : MonoBehaviour
{
    void LateUpdate()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject())
                return;

            var mousePos = Input.mousePosition;
            mousePos = new(mousePos.x, mousePos.y, Camera.main.nearClipPlane);
            var targetPos = (Vector2)Camera.main.ScreenToWorldPoint(mousePos);
            var client = Service.GetRequiredService<IGameApiClient>();
            _ = client.MoveAsync(targetPos.x, targetPos.y, destroyCancellationToken).AsTask().ContinueWith(t =>
            {
                var ex = t.Exception.Flatten().InnerException;
                if (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Unauthenticated)
                    _ = FaultScreen.CreateAsync(
                        message: "Connection timeout, need login again, will back to title screen.",
                        onClick: c => SceneManager.LoadScene("TitleScene"));
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
