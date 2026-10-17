using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using FluentValidation;
using FluentValidation.Results;

namespace DateBot.Domain.Common.Validation;

/// <summary>The single place where FluentValidation results become <see cref="Error"/>.</summary>
public static class ValidationExtensions
{
    public static Error ToError(this ValidationResult result, string code) =>
        Error.Validation(
            code,
            string.Join("; ", result.Errors.Select(e => e.ErrorMessage)),
            [.. result.Errors.Select(e => new ErrorDetail(e.PropertyName, e.ErrorCode, e.ErrorMessage))]);

    public static UnitResult<Error> ToResult(this ValidationResult result, string code) =>
        result.IsValid ? UnitResult.Success<Error>() : UnitResult.Failure(result.ToError(code));

    /// <summary>Validates <paramref name="input"/> and only on success builds the object.</summary>
    public static Result<T, Error> ValidateAndCreate<TInput, T>(
        this IValidator<TInput> validator, TInput input, string code, Func<TInput, T> create)
    {
        var result = validator.Validate(input);
        return result.IsValid
            ? Result.Success<T, Error>(create(input))
            : Result.Failure<T, Error>(result.ToError(code));
    }

    public static Result<T, Error> ValidateAndCreate<TInput, T>(
        this IValidator<TInput> validator, TInput input, string code, Func<TInput, Result<T, Error>> create)
    {
        var result = validator.Validate(input);
        return result.IsValid ? create(input) : Result.Failure<T, Error>(result.ToError(code));
    }
}
