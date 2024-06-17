namespace GameCore.Models;

public enum SyncCharacterAction : byte
{
    None = 0,
    Add = 1,
    Move = 2,
    Delete= 3,
    ChangeName = 12,
    ChangeSkin = 22,
}

public sealed record class SyncCharacterData(
    SyncCharacterAction Action,
    CharacterData Character);
