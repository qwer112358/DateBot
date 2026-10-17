using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.MiniApp.Enums;
using DateBot.Application.MiniApp.Services;
using DateBot.Application.Notifications;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.Processing.Models;
using DateBot.Application.Processing.Options;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.Models;
using DateBot.Application.UseCases.OwnerRequests;
using DateBot.Application.UseCases.Queries;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases.Handlers;

/// <summary>
/// Commands to the notifier bot. Only the Telegram user the userbot is logged in as may use them;
/// everyone else is ignored without an answer.
/// </summary>
internal sealed class HandleOwnerRequestHandler(
    IControlStateStore control,
    IUnitOfWork unitOfWork,
    IOwnerNotifier notifier,
    ICommandDispatcher dispatcher,
    AppCheckSignal appCheck,
    IOptions<ProcessingOptions> options,
    ILogger<HandleOwnerRequestHandler> logger)
    : ICommandHandler<HandleOwnerRequestCommand, Unit>
{
    private const string Help =
        "Команды: /start — включить и получать уведомления, /stop — экстренная остановка, /status — сводка, /likes — проверить лайки в приложении.";

    public Task<Result<Unit, Error>> HandleAsync(HandleOwnerRequestCommand command, CancellationToken ct) =>
        control.GetAsync(ct)
            .Bind(state => Authorize(state, command.Request))
            .Bind(state => ExecuteAsync(state, command.Request, ct)
                .OnFailureCompensate(error => Answer($"Не получилось: {error.Message}")))
            .Bind(answer => notifier.NotifyAsync(answer, ct))
            .Map(() => Unit.Value)
            .TapError(error => logger.LogWarning("Owner request {Request} rejected: {Error}", command.Request, error));

    private static Result<ControlState, Error> Authorize(ControlState state, OwnerRequest request) =>
        state.OwnerUserId.HasNoValue ? Result.Failure<ControlState, Error>(OwnerErrors.AccountUnknown)
        : state.OwnerUserId.GetValueOrThrow() != request.UserId ? Result.Failure<ControlState, Error>(OwnerErrors.Unauthorized)
        : state.OwnerChatId.HasValue && state.OwnerChatId.GetValueOrThrow() != request.ChatId && request is not StartRequest
            ? Result.Failure<ControlState, Error>(OwnerErrors.Unauthorized)
        : Result.Success<ControlState, Error>(state);

    private Task<Result<OwnerNotification, Error>> ExecuteAsync(ControlState state, OwnerRequest request, CancellationToken ct) =>
        request switch
        {
            StartRequest start => SaveAsync(state with { Paused = false, OwnerChatId = start.ChatId }, ct)
                .Map(() => (OwnerNotification)new TextNotification(StartAnswer(state))),
            StopRequest => SaveAsync(state with { Paused = true }, ct)
                .Map(() => (OwnerNotification)new TextNotification("Остановлено: новые действия не выполняются. /start — продолжить.")),
            StatusRequest => dispatcher
                .SendAsync<GetStatusQuery, StatusReport>(new GetStatusQuery(), ct)
                .Map(report => (OwnerNotification)new StatusNotification(report)),
            DraftDecisionRequest decision => dispatcher
                .SendAsync<SendReplyCommand, DraftDecision>(new SendReplyCommand(decision.MatchId, decision.Approve, decision.Channel), ct)
                .Map(result => (OwnerNotification)new TextNotification(DecisionAnswer(result, decision.Channel))),
            LookUpContactsRequest lookUp => dispatcher
                .SendAsync<LookUpContactsCommand, ImmutableArray<MessageChannel>>(new LookUpContactsCommand(lookUp.MatchId), ct)
                .Map(channels => (OwnerNotification)new TextNotification(channels.IsEmpty
                    ? "В приложении этот человек пока не найден. Напишите сами или попробуйте позже."
                    : "Нашёл. Выберите канал в новой карточке.")),
            ForgetRequest forget => dispatcher
                .SendAsync<ForgetMatchCommand, Unit>(new ForgetMatchCommand(forget.MatchId), ct)
                .Map(_ => (OwnerNotification)new TextNotification("Данные об этом человеке удалены.")),
            CheckLikesRequest => Task.FromResult(Answer(RequestLikesCheck(state))),
            LikeBackDecisionRequest decision => dispatcher
                .SendAsync<DecideLikeBackCommand, Unit>(new DecideLikeBackCommand(decision.MatchId, decision.Approve), ct)
                .Map(_ => (OwnerNotification)new TextNotification(LikeBackAnswer(state, decision.Approve))),
            _ => Task.FromResult(Answer(Help)),
        };

    /// <summary>Wakes the main loop; the summary comes once it has checked the app.</summary>
    private string RequestLikesCheck(ControlState state)
    {
        if (!options.Value.MiniApp.Enabled)
        {
            return "Mini App выключена (Processing:MiniApp:Enabled = false): лайки в приложении не проверяются.";
        }

        if (state.Paused)
        {
            return "Сервис остановлен: /start — продолжить.";
        }

        appCheck.Request(AppCheckRequest.Report);
        return "🔎 Проверяю лайки в приложении: новые придут карточками, потом сводка.";
    }

    private static string LikeBackAnswer(ControlState state, bool approve) => (approve, state.Paused) switch
    {
        (false, _) => "Пропущено. Лайк в приложении остаётся, ответить можно вручную.",
        (true, true) => "Принято. Сервис остановлен: лайк поставлю после /start.",
        _ => "Принято: ставлю лайк в ответ.",
    };

    private static string DecisionAnswer(DraftDecision decision, MessageChannel? channel) => (decision, channel) switch
    {
        (DraftDecision.Skipped, _) => "Пропущено.",
        (_, MessageChannel.Telegram) => "Отправлено в личку Telegram.",
        (_, MessageChannel.App) => "Отправлено в чат приложения.",
        (_, MessageChannel.BotChat) => "Отправлено через бота.",
        _ => "Отправлено.",
    };

    private static string StartAnswer(ControlState before) =>
        before.OwnerChatId.HasNoValue ? "Готово: уведомления будут приходить сюда. " + Help
        : before.Paused ? "Обработка возобновлена."
        : "Сервис уже работает. " + Help;

    private Task<UnitResult<Error>> SaveAsync(ControlState state, CancellationToken ct) =>
        control.SaveAsync(state, ct).Bind(() => unitOfWork.CommitAsync(ct));

    private static Result<OwnerNotification, Error> Answer(string text) =>
        Result.Success<OwnerNotification, Error>(new TextNotification(text));
}
