using System.Collections.Immutable;
using CSharpFunctionalExtensions;

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

/// <summary>One validation violation: which property, which rule, human-readable message.</summary>
public sealed record ErrorDetail(string PropertyName, string ErrorCode, string Message);

/// <summary>Typed error used by every <c>Result</c> in the solution instead of exceptions.</summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public ImmutableArray<ErrorDetail> Details { get; init; } = [];

    /// <summary>For <see cref="ErrorType.RateLimited"/>: how long to wait before retrying.</summary>
    public Maybe<TimeSpan> RetryAfter { get; init; } = Maybe<TimeSpan>.None;

    public static Error Validation(string code, string message, ImmutableArray<ErrorDetail> details) =>
        new(code, message, ErrorType.Validation) { Details = details };

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error External(string code, string message) => new(code, message, ErrorType.External);

    public static Error RateLimited(string code, string message, TimeSpan retryAfter) =>
        new(code, message, ErrorType.RateLimited) { RetryAfter = retryAfter };

    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);

    public override string ToString() => $"{Code}: {Message}";
}
