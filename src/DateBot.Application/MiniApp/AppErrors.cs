using DateBot.Application.Abstractions;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;

namespace DateBot.Application.MiniApp;

/// <summary>Errors the dating-app adapter returns through <see cref="Ports.ITashdateApp"/>.</summary>
public static class AppErrors
{
    public const string LikeLimitCode = "miniapp.like_limit";

    public const string AuthRejectedCode = "miniapp.auth.unavailable";

    public static readonly Error ChatUnavailable = Error.Conflict(
        "miniapp.chat_unavailable", "Чат в приложении закрыт для этой анкеты; сообщение не отправлено");

    /// <summary>Retryable: the app did not answer or answered 5xx; nothing was changed.</summary>
    public static Error Unavailable(string details) => Error.External(
        "miniapp.unavailable", $"Приложение знакомств недоступно: {details}");

    /// <summary>Retryable: the app does not accept the Telegram sign-in even after a fresh one.</summary>
    public static Error AuthRejected(int status) => Error.External(
        AuthRejectedCode, $"Приложение не принимает вход через Telegram (HTTP {status})");

    /// <summary>The daily like limit of the app; likes wait, everything else goes on.</summary>
    public static Error LikeLimit(int limit, TimeSpan retryAfter) => Error.RateLimited(
        LikeLimitCode, $"В приложении закончились лайки на сегодня (лимит {limit})", retryAfter);

    public static Error TooManyRequests(TimeSpan retryAfter) => Error.RateLimited(
        "miniapp.rate_limited", "Приложение просит делать запросы реже", retryAfter);

    /// <summary>The owner's own profile is locked by the app (moderation, block): wait instead of failing every like.</summary>
    public static Error AccountLocked(string reason) => Error.RateLimited(
        "miniapp.account_locked", $"Приложение ограничило вашу анкету: {reason}", TimeSpan.FromHours(6));

    public static Error ProfileNotFound(AppProfileId id) => Error.NotFound(
        "miniapp.profile_not_found", $"Анкета {id} в приложении не найдена");

    public static Error Refused(int status, string details) => Error.Conflict(
        "miniapp.refused", $"Приложение отклонило запрос (HTTP {status}): {details}");

    public static Error BadResponse(string details) => Error.Unexpected(
        "miniapp.bad_response", $"Непонятный ответ приложения: {details}");

    public static Error SendOutcomeUnknown(string details) => DeliveryErrors.OutcomeUnknown(details);
}
