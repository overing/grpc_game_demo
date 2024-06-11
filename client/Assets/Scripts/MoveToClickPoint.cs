using UnityEngine;

public sealed class MoveToClickPoint : MonoBehaviour
{
    void LateUpdate()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (!TryGetComponent<CharacterController2D>(out var character))
                return;

            var mousePos = Input.mousePosition;
            mousePos = new(mousePos.x, mousePos.y, Camera.main.nearClipPlane);
            var targetPos = (Vector2)Camera.main.ScreenToWorldPoint(mousePos);
            character.SmoothMoveTo(targetPos);
        }
    }
}
