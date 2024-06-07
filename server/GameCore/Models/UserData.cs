using System;

namespace GameCore.Models;

public sealed record class UserData(
    Guid ID,
    string Name,
    string Email);
