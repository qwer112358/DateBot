using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Infrastructure.Notifier.Models;

public sealed record FormattedNotification(string Html, ImmutableArray<InlineAction> Buttons, Maybe<PhotoFileId> Photo);
