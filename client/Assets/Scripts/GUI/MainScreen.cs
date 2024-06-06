using UnityEngine;

public sealed class MainScreen : MonoBehaviour
{
    Player _player;

    void Start()
    {
        _player = Service.GetRequiredService<Player>();
    }

    void OnGUI()
    {
        using (new GUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(Screen.width), GUILayout.Height(Screen.height)))
        {
            using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label("Name:", GUILayout.Width(GUI.skin.label.fontSize * 5));
                GUILayout.TextField(_player.Name, GUILayout.ExpandWidth(true));
            }
            using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label("ServerTime:", GUILayout.Width(GUI.skin.label.fontSize * 6));
                GUILayout.TextField(_player.ServerTime.ToString(), GUILayout.ExpandWidth(true));
            }
        }
    }
}
