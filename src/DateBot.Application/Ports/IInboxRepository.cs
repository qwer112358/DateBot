using CSharpFunctionalExtensions;
using DateBot.Domain.Common;
using DateBot.Domain.Inbox;
using DateBot.Domain.Shared;

namespace DateBot.Application.Ports;

public interface IInboxRepository
{
    Task<Result<Maybe<InboxMessage>, Error>> FindAsync(BotMessageId id, CancellationToken ct);

    Task<UnitResult<Error>> AddAsync(InboxMessage message, CancellationToken ct);

    Task<UnitResult<Error>> RemoveAsync(BotMessageId id, CancellationToken ct);

    Task<Result<IReadOnlyDictionary<InboxStatus, int>, Error>> CountByStatusAsync(CancellationToken ct);

    Task<Result<int, Error>> PurgeSentBeforeAsync(DateTimeOffset before, CancellationToken ct);
}
