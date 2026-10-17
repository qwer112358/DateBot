using DateBot.Application.Notifications.Enums;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Notifications;

public sealed record ActionFailedNotification(MatchId MatchId, Profile Profile, Error Reason)
    : OwnerNotification(NotificationImportance.Error);
