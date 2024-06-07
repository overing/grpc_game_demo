using System;
using UnityEngine;

public sealed class FaultScreen : MonoBehaviour
{
    string _errorMessage;

    public string ErrorMessage
    {
        set => _errorMessage = value;
    }

    public event Action<FaultScreen> OkClicked;

    void OnGUI()
    {
        bool clickOk;
        using (new GUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(Screen.width), GUILayout.Height(Screen.height)))
        {
            GUILayout.FlexibleSpace();

            GUILayout.Label("Error", GUILayout.Width(GUI.skin.label.fontSize * 6));
            GUILayout.TextField(_errorMessage, GUILayout.ExpandWidth(true));
            clickOk = GUILayout.Button("OK", GUILayout.ExpandWidth(false));

            GUILayout.FlexibleSpace();
        }
        if (clickOk)
            OkClicked?.Invoke(this);
    }
}
