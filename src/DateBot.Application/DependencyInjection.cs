using System.Collections.Immutable;
using DateBot.Application.Abstractions.Events;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.MiniApp.Services;
using DateBot.Application.Parsing.Models;
using DateBot.Application.Parsing.Options;
using DateBot.Application.Processing.Services;
using DateBot.Application.Replies.Services;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.EventHandlers;
using DateBot.Application.UseCases.Handlers;
using DateBot.Application.UseCases.Models;
using DateBot.Application.UseCases.Queries;
using DateBot.Application.UseCases.Services;
using DateBot.Application.UseCases.Validators;
using DateBot.Domain.Matches.Enums;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DateBot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<ProcessBotMessageCommandValidator>(ServiceLifetime.Singleton, includeInternalTypes: true);

        services.AddSingleton<ICommandDispatcher, ScopedCommandDispatcher>();
        services.AddSingleton(sp => ParsingRules.From(sp.GetRequiredService<IOptions<ParsingOptions>>().Value));

        services.AddSingleton<AppCheckSignal>();
        services.AddSingleton<PendingDraftEdit>();

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler, NotifyOwner>();

        services.AddScoped<ActionPacer>();
        services.AddScoped<ReplyComposer>();
        services.AddScoped<LikeBack>();
        services.AddScoped<EnrichFromApp>();
        services.AddScoped<ProfilePhotos>();
        services.AddScoped<GenerateAndSendReply>();

        services.AddCommandHandler<ProcessBotMessageCommand, ProcessOutcome, ProcessBotMessageHandler>();
        services.AddCommandHandler<CatchUpHistoryCommand, CatchUpReport, CatchUpHistoryHandler>();
        services.AddCommandHandler<SyncAppLikesCommand, AppSyncReport, SyncAppLikesHandler>();
        services.AddCommandHandler<ProcessAppLikeCommand, ProcessOutcome, ProcessAppLikeHandler>();
        services.AddCommandHandler<SendReplyCommand, DraftDecision, SendReplyHandler>();
        services.AddCommandHandler<DecideLikeBackCommand, Unit, DecideLikeBackHandler>();
        services.AddCommandHandler<ReviseDraftCommand, Unit, ReviseDraftHandler>();
        services.AddCommandHandler<OfferDraftsCommand, int, OfferDraftsHandler>();
        services.AddCommandHandler<OfferMatchesCommand, MatchesReport, OfferMatchesHandler>();
        services.AddCommandHandler<LookUpContactsCommand, ImmutableArray<MessageChannel>, LookUpContactsHandler>();
        services.AddCommandHandler<GetStatusQuery, StatusReport, GetStatusHandler>();
        services.AddCommandHandler<HandleOwnerRequestCommand, Unit, HandleOwnerRequestHandler>();
        services.AddCommandHandler<RememberOwnerAccountCommand, Unit, RememberOwnerAccountHandler>();
        services.AddCommandHandler<ForgetMatchCommand, Unit, ForgetMatchHandler>();
        services.AddCommandHandler<PurgeExpiredDataCommand, PurgeReport, PurgeExpiredDataHandler>();

        return services;
    }
}
