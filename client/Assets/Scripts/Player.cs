using System;
using GameCore.Models;
using UnityEngine;

public sealed class Player
{
    double _loadTime;
    PlayerData _data;

    public string Name => _data?.Name;
    public DateTimeOffset? ServerNow => _data?.ServerTime.AddSeconds(Time.realtimeSinceStartupAsDouble - _loadTime);

    public void Load(PlayerData data)
    {
        _loadTime = Time.realtimeSinceStartupAsDouble;
        _data = data;
    }
}
