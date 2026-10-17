using CSharpFunctionalExtensions;
using DateBot.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace DateBot.Application.Abstractions;

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
