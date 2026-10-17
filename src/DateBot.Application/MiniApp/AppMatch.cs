using CSharpFunctionalExtensions;

namespace DateBot.Application.MiniApp;

/// <summary>A match as the app lists it; the username may be missing.</summary>
public sealed record AppMatch(long ProfileId, string Name, Maybe<string> Username);
