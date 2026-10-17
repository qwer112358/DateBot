using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using DateBot.Application.Abstractions;
using DateBot.Application.Parsing;
using DateBot.Application.Processing;
using DateBot.Application.Replies;
using DateBot.Application.UseCases;

namespace DateBot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<ProcessBotMessageCommandValidator>(ServiceLifetime.Singleton, includeInternalTypes: true);

        services.AddSingleton<ICommandDispatcher, ScopedCommandDispatcher>();
        services.AddSingleton(sp => ParsingRules.From(sp.GetRequiredService<IOptions<ParsingOptions>>().Value));

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler, NotifyOwner>();

        services.AddScoped<ActionPacer>();
        services.AddScoped<ReplyComposer>();
        services.AddScoped<LikeBack>();
        services.AddScoped<GenerateAndSendReply>();

        services.AddCommandHandler<ProcessBotMessageCommand, ProcessOutcome, ProcessBotMessageHandler>();
        services.AddCommandHandler<CatchUpHistoryCommand, CatchUpReport, CatchUpHistoryHandler>();
        services.AddCommandHandler<SendReplyCommand, DraftDecision, SendReplyHandler>();
        services.AddCommandHandler<GetStatusQuery, StatusReport, GetStatusHandler>();
        services.AddCommandHandler<HandleOwnerRequestCommand, Unit, HandleOwnerRequestHandler>();
        services.AddCommandHandler<RememberOwnerAccountCommand, Unit, RememberOwnerAccountHandler>();
        services.AddCommandHandler<ForgetMatchCommand, Unit, ForgetMatchHandler>();
        services.AddCommandHandler<PurgeExpiredDataCommand, PurgeReport, PurgeExpiredDataHandler>();

        return services;
    }
}
