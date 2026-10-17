using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Abstractions.Messaging;

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<Result<TResult, Error>> HandleAsync(TCommand command, CancellationToken ct);
}
