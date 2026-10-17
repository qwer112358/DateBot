namespace DateBot.Application.Parsing.Enums;

public enum UnknownMessagePolicy
{
    /// <summary>Unrecognised messages go to ParseFailed and you get a notification.</summary>
    ParseFailed,

    /// <summary>Unrecognised messages are stored as Ignored without a notification.</summary>
    Ignore,
}
