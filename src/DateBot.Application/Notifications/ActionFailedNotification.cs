using DateBot.Domain.Common;
using DateBot.Domain.Matches;

namespace DateBot.Application.Notifications;

public sealed record ActionFailedNotification(MatchId MatchId, Profile Profile, Error Reason)
    : OwnerNotification(NotificationImportance.Error);
