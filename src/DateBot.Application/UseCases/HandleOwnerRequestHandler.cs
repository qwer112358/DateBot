using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Notifications;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Domain.Common;
using Microsoft.Extensions.Logging;

namespace DateBot.Application.UseCases;

/// <summary>
/// Commands to the notifier bot. Only the Telegram user the userbot is logged in as may use them;
/// everyone else is ignored without an answer.
/// </summary>
internal sealed class HandleOwnerRequestHandler(
    IControlStateStore control,
    IUnitOfWork unitOfWork,
    IOwnerNotifier notifier,
    ICommandDispatcher dispatcher,
    ILogger<HandleOwnerRequestHandler> logger)
    : ICommandHandler<HandleOwnerRequestCommand, Unit>
{
    private const string Help = "Команды: /start — включить и получать уведомления, /stop — экстренная остановка, /status — сводка.";

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
                .SendAsync<SendReplyCommand, DraftDecision>(new SendReplyCommand(decision.MatchId, decision.Approve), ct)
                .Map(result => (OwnerNotification)new TextNotification(result == DraftDecision.Sent ? "Отправлено." : "Пропущено.")),
            ForgetRequest forget => dispatcher
                .SendAsync<ForgetMatchCommand, Unit>(new ForgetMatchCommand(forget.MatchId), ct)
                .Map(_ => (OwnerNotification)new TextNotification("Данные об этом человеке удалены.")),
            _ => Task.FromResult(Answer(Help)),
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
