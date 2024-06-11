using GameClient;
using UnityEngine;

public sealed class SendMoveToClickPoint : MonoBehaviour
{
    void LateUpdate()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.LogWarning("MoveAsync");
            var mousePos = Input.mousePosition;
            mousePos = new(mousePos.x, mousePos.y, Camera.main.nearClipPlane);
            var targetPos = (Vector2)Camera.main.ScreenToWorldPoint(mousePos);
            var client = Service.GetRequiredService<IGameApiClient>();
            _ = client.MoveAsync(targetPos.x, targetPos.y, destroyCancellationToken);
        }
    }
}
