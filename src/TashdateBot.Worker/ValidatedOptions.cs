using FluentValidation;
using Microsoft.Extensions.Options;

namespace TashdateBot.Worker;

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

internal static class ValidatedOptionsExtensions
{
    public static IServiceCollection AddValidatedOptions<T>(this IServiceCollection services, IConfiguration configuration, string section)
        where T : class
    {
        services.AddOptions<T>().Bind(configuration.GetSection(section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<T>, FluentValidateOptions<T>>();
        return services;
    }
}
