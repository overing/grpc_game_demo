using System;

namespace GameCore.Models;

public sealed record class ChatData(
    string Sender,
    string Message);
