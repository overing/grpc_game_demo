using System;

namespace GameCore.Models;

public sealed record class CharacterData(
    Guid ID,
    string Name,
    int Skin,
    (float x, float y) Position);
