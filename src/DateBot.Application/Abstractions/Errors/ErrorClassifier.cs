using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Abstractions.Errors;

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
