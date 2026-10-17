using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Domain.Matches;

namespace DateBot.Infrastructure.Notifier;

public sealed record FormattedNotification(string Html, ImmutableArray<InlineAction> Buttons, Maybe<PhotoFileId> Photo);
