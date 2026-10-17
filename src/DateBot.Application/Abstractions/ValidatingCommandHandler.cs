using CSharpFunctionalExtensions;
using DateBot.Domain.Common;
using FluentValidation;
using FluentValidation.Results;

namespace DateBot.Application.Abstractions;

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
