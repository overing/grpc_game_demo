using GameCore.Models;

public sealed class Player
{
    UserData _data;

    public string Name => _data?.Name;

    public void Load(UserData data)
    {
        _data = data;
    }
}
