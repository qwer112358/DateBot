using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

public static class MatchErrors
{
    public static readonly Error AlreadyMessaged = Error.Conflict(
        "match.already_messaged", "Этому человеку уже отправлено или подготовлено автосообщение");

    public static readonly Error LikeBackNotAllowed = Error.Conflict(
        "match.like_back_not_allowed", "Лайк в ответ ставится только на входящий лайк");

    public static readonly Error ActionInProgress = Error.Conflict(
        "match.action_in_progress", "У анкеты уже есть незавершённое действие");

    public static readonly Error NoDraft = Error.Conflict(
        "match.no_draft", "У анкеты нет черновика сообщения");

    public static readonly Error SendOutcomeUnknown = Error.Conflict(
        "match.send_outcome_unknown",
        "Отправка сообщения прервалась перезапуском; повтор не делаем, чтобы не написать дважды");

    public static Error ChannelUnavailable(MessageChannel channel, string reason) => Error.Conflict(
        "match.channel_unavailable", $"Канал {channel} недоступен: {reason}");

    public static Error InvalidTransition(MatchStatus from, MatchStatus to) => Error.Conflict(
        "match.invalid_transition", $"Переход статуса {from} → {to} не разрешён");

    public static Error InvalidState(MatchStatus status, string operation) => Error.Conflict(
        "match.invalid_state", $"Действие «{operation}» недоступно в статусе {status}");

    public static Error NoPendingAction(ActionType type) => Error.Conflict(
        "match.no_pending_action", $"Нет незавершённого действия {type}");

    public static Error NotFound(MatchId id) => Error.NotFound(
        "match.not_found", $"Анкета {id.Short} не найдена");
}
