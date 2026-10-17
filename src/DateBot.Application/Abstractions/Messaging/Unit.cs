namespace DateBot.Application.Abstractions.Messaging;

/// <summary>Result value for commands that return nothing.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value;
}
