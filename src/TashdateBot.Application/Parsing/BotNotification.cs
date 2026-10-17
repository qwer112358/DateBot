using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using TashdateBot.Application.Telegram;

namespace TashdateBot.Application.Parsing;

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

public abstract record BotNotification(RawBotMessage Source);

/// <summary>"Someone liked you": scenario A.</summary>
public sealed record LikeNotification(RawBotMessage Source, ParsedProfile Profile) : BotNotification(Source);

/// <summary>"Mutual like": scenario B.</summary>
public sealed record MutualNotification(RawBotMessage Source, ParsedProfile Profile) : BotNotification(Source);

/// <summary>Service message of the bot; stored, no reaction.</summary>
public sealed record IgnoredNotification(RawBotMessage Source, string Reason) : BotNotification(Source);

/// <summary>Not recognised; goes to ParseFailed for manual review.</summary>
public sealed record UnknownNotification(RawBotMessage Source, string Reason) : BotNotification(Source);
