namespace DateBot.Application.Abstractions;

/// <summary>Result value for commands that return nothing.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value;
}
