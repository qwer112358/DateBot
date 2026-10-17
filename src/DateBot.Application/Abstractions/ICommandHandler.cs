using CSharpFunctionalExtensions;
using DateBot.Domain.Common;

namespace DateBot.Application.Abstractions;

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<Result<TResult, Error>> HandleAsync(TCommand command, CancellationToken ct);
}
