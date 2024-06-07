
using System;

namespace GameCore.Models;

public sealed record class LoginData(
    DateTimeOffset ServerTime,
    UserData User);
