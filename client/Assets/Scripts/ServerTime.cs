using System;
using UnityEngine;

public sealed class ServerTime
{
    double _loadTime;
    DateTimeOffset _serverTime;
    public DateTimeOffset? Now => _serverTime.AddSeconds(Time.realtimeSinceStartupAsDouble - _loadTime);

    public void Load(DateTimeOffset serverTime)
    {
        _loadTime = Time.realtimeSinceStartupAsDouble;
        _serverTime = serverTime;
    }
}