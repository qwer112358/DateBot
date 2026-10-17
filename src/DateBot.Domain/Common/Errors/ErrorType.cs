namespace DateBot.Domain.Common.Errors;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    External,
    RateLimited,
    Unexpected,
}
