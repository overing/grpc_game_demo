using System;

namespace GameCore.Models;

public sealed record class UserData(
    Guid ID,
    string Name,
    string Email,
    byte Skin,
    float PosX,
    float PosY);
