using FluentValidation;
using Microsoft.Extensions.Options;

namespace DateBot.Worker;

/// <summary>Runs the FluentValidation validator of an options class at start-up (<c>ValidateOnStart</c>).</summary>
internal sealed class FluentValidateOptions<T>(IValidator<T> validator) : IValidateOptions<T>
    where T : class
{
    public ValidateOptionsResult Validate(string? name, T options)
    {
        var result = validator.Validate(options);
        return result.IsValid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(result.Errors.Select(e => $"{typeof(T).Name}.{e.PropertyName}: {e.ErrorMessage}"));
    }
}
