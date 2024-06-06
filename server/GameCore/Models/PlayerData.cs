using System;

namespace GameCore.Models
{
    public sealed class PlayerData
    {
        public DateTimeOffset ServerTime { get; set; }
        public string Name { get; set; } = null!;
    }
}
