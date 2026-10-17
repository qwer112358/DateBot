using TashdateBot.Domain.Common;

namespace TashdateBot.Application.Abstractions;

public static class ServiceErrors
{
    public static readonly Error Paused = Error.Conflict("service.paused", "Обработка остановлена командой /stop");

    public static readonly Error OwnerNotRegistered = Error.NotFound(
        "service.owner_not_registered", "Бот-уведомитель ещё не знает ваш чат: напишите ему /start");

    public static Error Storage(string details) => Error.Unexpected("storage.failure", details);

    public static Error CorruptedData(string details) => Error.Unexpected("storage.corrupted", details);
}

public static class ErrorClassifier
{
    /// <summary>
    /// Retryable errors leave the message unprocessed (cursor stays) so it is retried later;
    /// any other error fails the match and moves on.
    /// </summary>
    public static bool IsRetryable(Error error) =>
        error.Type == ErrorType.RateLimited
        || error.Code == ServiceErrors.Paused.Code
        || error.Code.EndsWith(".unavailable", StringComparison.Ordinal);
}
