using CSharpFunctionalExtensions;
using DateBot.Domain.Common;

namespace DateBot.Application.Abstractions;

/// <summary>Lightweight dispatcher: runs every command in its own DI scope (own DbContext).</summary>
public interface ICommandDispatcher
{
    Task<Result<TResult, Error>> SendAsync<TCommand, TResult>(TCommand command, CancellationToken ct)
        where TCommand : ICommand<TResult>;
}
