using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace DateBot.Application.MiniApp;

/// <summary>A profile from the app as readable text: codes are already turned into labels by the adapter.</summary>
/// <param name="Facts">Lines like "Рост: 172 см" built from the structured fields.</param>
/// <param name="PhotoFile">First photo file of the profile, if any.</param>
/// <param name="RawText">Everything above as one readable block, kept as the original text of the profile.</param>
public sealed record AppProfile(
    long Id,
    string Name,
    Maybe<int> Age,
    Maybe<string> City,
    Maybe<string> Bio,
    ImmutableArray<string> Interests,
    ImmutableArray<string> Facts,
    Maybe<string> PhotoFile,
    string RawText);
