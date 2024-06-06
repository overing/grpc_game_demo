using System;
using GameCore.Models;
using UnityEngine;

public sealed class Player
{
    UserData _data;

    public string Name => _data?.Name;

    public void Load(UserData data)
    {
        _data = data;
    }
}
