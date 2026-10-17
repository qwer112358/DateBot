using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Abstractions.Messaging;

/// <summary>Lightweight dispatcher: runs every command in its own DI scope (own DbContext).</summary>
public interface ICommandDispatcher
{
    Task<Result<TResult, Error>> SendAsync<TCommand, TResult>(TCommand command, CancellationToken ct)
        where TCommand : ICommand<TResult>;
}
