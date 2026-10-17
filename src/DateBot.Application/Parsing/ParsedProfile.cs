using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace DateBot.Application.Parsing;

/// <summary>Profile fields as plain text, before domain validation.</summary>
public sealed record ParsedProfile(
    string Name,
    Maybe<int> Age,
    Maybe<string> City,
    Maybe<string> Bio,
    ImmutableArray<string> Interests,
    Maybe<string> PhotoId,
    Maybe<string> Username,
    Maybe<string> PersonId);
