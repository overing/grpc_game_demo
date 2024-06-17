using System;

namespace GameCore.Models;

public sealed record class CharacterData(
    Guid ID,
    string Name,
    byte Skin,
    PointFloat Position)
{
    public static string CutNameForDisplay(string name)
        => name.Length > 7 ? name[..7] + "..." : name;
}
