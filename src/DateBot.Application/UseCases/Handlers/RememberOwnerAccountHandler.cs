using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.UseCases.Commands;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.UseCases.Handlers;

internal sealed class RememberOwnerAccountHandler(IControlStateStore control, IUnitOfWork unitOfWork)
    : ICommandHandler<RememberOwnerAccountCommand, Unit>
{
    public Task<Result<Unit, Error>> HandleAsync(RememberOwnerAccountCommand command, CancellationToken ct) =>
        control.GetAsync(ct)
            .Map(state => state.OwnerUserId.HasValue && state.OwnerUserId.GetValueOrThrow() == command.UserId
                ? state
                : state with { OwnerUserId = command.UserId, OwnerChatId = Maybe<long>.None })
            .Bind(state => control.SaveAsync(state, ct))
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Map(() => Unit.Value);
}
