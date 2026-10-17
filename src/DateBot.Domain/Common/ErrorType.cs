namespace DateBot.Domain.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    External,
    RateLimited,
    Unexpected,
}
