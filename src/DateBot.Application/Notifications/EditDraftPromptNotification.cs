using DateBot.Application.Notifications.Enums;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Notifications;

/// <summary>Answer to «✏️ Изменить»: the current text, ready to copy, change and send back.</summary>
public sealed record EditDraftPromptNotification(ReplyText Draft) : OwnerNotification(NotificationImportance.Reply);
