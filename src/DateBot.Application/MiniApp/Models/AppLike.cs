namespace DateBot.Application.MiniApp.Models;

/// <summary>An incoming like the app shows openly (not hidden behind premium).</summary>
public sealed record AppLike(long ProfileId, string Name);
