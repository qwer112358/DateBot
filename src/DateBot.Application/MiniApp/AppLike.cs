namespace DateBot.Application.MiniApp;

/// <summary>An incoming like the app shows openly (not hidden behind premium).</summary>
public sealed record AppLike(long ProfileId, string Name);
