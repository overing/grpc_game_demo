
using System;
using GameCore.Models;

namespace GameClient
{
    public sealed record class LoginData(
        DateTimeOffset ServerTime,
        UserData User);
}
