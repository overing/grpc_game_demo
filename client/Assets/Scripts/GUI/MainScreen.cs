using UnityEngine;

public sealed class MainScreen : MonoBehaviour
{
    ServerTime _serverTime;
    Player _player;

    void Start()
    {
        _serverTime = Service.GetRequiredService<ServerTime>();
        _player = Service.GetRequiredService<Player>();
    }

    void OnGUI()
    {
        using (new GUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(Screen.width), GUILayout.Height(Screen.height)))
        {
            GUILayout.FlexibleSpace();

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

            GUILayout.FlexibleSpace();
        }
    }
}
