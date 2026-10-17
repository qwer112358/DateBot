using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace DateBot.Application.Parsing;

/// <summary>Profile fields as plain text, before domain validation.</summary>
/// <param name="PersonId">Stable id the bot shows, if the rules extract one.</param>
/// <param name="AppId">Profile id in the dating app, when the profile came from the app.</param>
public sealed record ParsedProfile(
    string Name,
    Maybe<int> Age,
    Maybe<string> City,
    Maybe<string> Bio,
    ImmutableArray<string> Interests,
    Maybe<string> PhotoId,
    Maybe<string> Username,
    Maybe<string> PersonId,
    ImmutableArray<string> Facts = default,
    Maybe<long> AppId = default);
