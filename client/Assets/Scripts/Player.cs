using System;
using GameCore.Models;

public sealed class Player
{
    UserData _data;

    public Guid ID => _data?.ID ?? Guid.Empty;
    public string Name => _data?.Name;

    public void Load(UserData data)
    {
        _data = data;
    }
}
