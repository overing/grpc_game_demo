using System;
using GameCore.Models;

public sealed class Player
{
    PlayerData _data;

    public string Name => _data?.Name;
    public DateTimeOffset? ServerTime => _data?.ServerTime;

    public void Load(PlayerData data) => _data = data;
}
