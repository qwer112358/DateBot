using CSharpFunctionalExtensions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using TashdateBot.Domain.Common;

namespace TashdateBot.Application.Abstractions;

/// <summary>Result value for commands that return nothing.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value;
}

public interface ICommand<TResult>;

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<Result<TResult, Error>> HandleAsync(TCommand command, CancellationToken ct);
}

/// <summary>Lightweight dispatcher: runs every command in its own DI scope (own DbContext).</summary>
public interface ICommandDispatcher
{
    Task<Result<TResult, Error>> SendAsync<TCommand, TResult>(TCommand command, CancellationToken ct)
        where TCommand : ICommand<TResult>;
}

internal sealed class ScopedCommandDispatcher(IServiceScopeFactory scopes) : ICommandDispatcher
{
    public async Task<Result<TResult, Error>> SendAsync<TCommand, TResult>(TCommand command, CancellationToken ct)
        where TCommand : ICommand<TResult>
    {
        await using var scope = scopes.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<TCommand, TResult>>();
        return await handler.HandleAsync(command, ct);
    }
}

/// <summary>Decorator: validates the command with FluentValidation; a failure is a Validation error, not an exception.</summary>
internal sealed class ValidatingCommandHandler<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IEnumerable<IValidator<TCommand>> validators)
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<Result<TResult, Error>> HandleAsync(TCommand command, CancellationToken ct)
    {
        var failures = validators.SelectMany(v => v.Validate(command).Errors).ToList();
        return failures.Count == 0
            ? await inner.HandleAsync(command, ct)
            : Result.Failure<TResult, Error>(new ValidationResult(failures).ToError($"{typeof(TCommand).Name}.invalid"));
    }
}

public static class CommandRegistration
{
    public static IServiceCollection AddCommandHandler<TCommand, TResult, THandler>(this IServiceCollection services)
        where TCommand : ICommand<TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        services.AddScoped<THandler>();
        services.AddScoped<ICommandHandler<TCommand, TResult>>(sp => new ValidatingCommandHandler<TCommand, TResult>(
            sp.GetRequiredService<THandler>(), sp.GetServices<IValidator<TCommand>>()));
        return services;
    }
}
