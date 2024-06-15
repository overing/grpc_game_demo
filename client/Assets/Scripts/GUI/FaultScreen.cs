using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class FaultScreen : MonoBehaviour
{
    [SerializeField]
    Text _messageText;

    [SerializeField]
    Button _okButton;

    public string Message
    {
        set => _messageText.text = value;
    }

    public event Action<FaultScreen> OkClicked;

    void Start()
    {
        _okButton.onClick.AddListener(() => OkClicked?.Invoke(this));
    }

    public static async UniTask<FaultScreen> CreateAsync(string message, Action<FaultScreen> onClick)
    {
        var prefab = (GameObject)await Resources.LoadAsync<GameObject>("Prefabs/GUI/FaultScreen");
        var instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        var screen = instance.GetComponent<FaultScreen>();
        screen.Message = message;
        screen.OkClicked += onClick;
        return screen;
    }
}
