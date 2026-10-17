using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Infrastructure.Notifier.Models;

/// <param name="Album">The profile's other photos, sent as an album just above the card.</param>
public sealed record FormattedNotification(
    string Html, ImmutableArray<InlineAction> Buttons, Maybe<PhotoFileId> Photo, ImmutableArray<PhotoFileId> Album = default);
