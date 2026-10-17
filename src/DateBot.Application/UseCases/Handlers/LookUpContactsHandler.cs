using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.Notifications;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Services;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.UseCases.Handlers;

/// <summary>When the person is found now, the draft card comes again, this time with the channel buttons.</summary>
internal sealed class LookUpContactsHandler(IMatchRepository matches, GenerateAndSendReply reply, IOwnerNotifier notifier)
    : ICommandHandler<LookUpContactsCommand, ImmutableArray<MessageChannel>>
{
    public Task<Result<ImmutableArray<MessageChannel>, Error>> HandleAsync(LookUpContactsCommand command, CancellationToken ct) =>
        MatchId.Create(command.MatchId)
            .Bind(id => matches.FindByIdAsync(id, ct).Bind(found => found.ToResult(MatchErrors.NotFound(id))))
            .Bind(match => reply.LookUpAsync(match, ct)
                .Tap(channels => channels.IsEmpty
                    ? Task.CompletedTask
                    : notifier.NotifyAsync(
                        new DraftReadyNotification(
                            match.Id,
                            match.Kind,
                            match.Profile,
                            match.DraftMessage.GetValueOrThrow().Text.GetValueOrThrow(),
                            match.IsMutual,
                            channels),
                        ct)));
}
