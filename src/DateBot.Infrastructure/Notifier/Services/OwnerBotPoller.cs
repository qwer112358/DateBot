using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.OwnerRequests;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.Enums;
using DateBot.Infrastructure.Notifier.Models;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DateBot.Infrastructure.Notifier.Services;

/// <summary>Long-polls the notifier bot and turns commands and button presses into <see cref="OwnerRequest"/>.</summary>
public sealed class OwnerBotPoller(NotifierBot bot, ILogger<OwnerBotPoller> logger)
{
    private static readonly UpdateType[] AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery];
    private int _offset;

    public bool Enabled => bot.Client.HasValue;

    public async Task<Result<ImmutableArray<OwnerRequest>, Error>> ReceiveAsync(CancellationToken ct)
    {
        if (bot.Client.HasNoValue)
        {
            return ImmutableArray<OwnerRequest>.Empty;
        }

        var client = bot.Client.GetValueOrThrow();
        try
        {
            var updates = await client.GetUpdates(_offset, limit: 50, timeout: 30, allowedUpdates: AllowedUpdates, cancellationToken: ct);
            if (updates.Length > 0)
            {
                _offset = updates.Max(u => u.Id) + 1;
            }

            var requests = ImmutableArray.CreateBuilder<OwnerRequest>();
            foreach (var update in updates)
            {
                var request = update switch
                {
                    { Message: { Text: { } text, From: { } from } message } => Maybe.From(ParseCommand(message.Chat.Id, from.Id, text)),
                    { CallbackQuery: { Data: { } data, Message: { } message } query } => await ParseCallbackAsync(client, query, message, data, ct),
                    _ => Maybe<OwnerRequest>.None,
                };
                if (request.HasValue)
                {
                    requests.Add(request.GetValueOrThrow());
                }
            }

            return requests.ToImmutable();
        }
        catch (Exception ex) when (ex is RequestException or HttpRequestException)
        {
            return Result.Failure<ImmutableArray<OwnerRequest>, Error>(Error.External("notifier.unavailable", ex.Message));
        }
    }

    public static OwnerRequest ParseCommand(long chatId, long userId, string text)
    {
        var command = text.Trim().Split(' ', 2)[0].Split('@')[0].ToLowerInvariant();
        return command switch
        {
            "/start" => new StartRequest(chatId, userId),
            "/stop" => new StopRequest(chatId, userId),
            "/status" => new StatusRequest(chatId, userId),
            "/likes" => new CheckLikesRequest(chatId, userId),
            "/cancel" => new CancelRequest(chatId, userId),
            "/drafts" => new DraftsRequest(chatId, userId),
            "/matches" => new MatchesRequest(chatId, userId),
            _ => new UnknownRequest(chatId, userId, text),
        };
    }

    /// <summary>
    /// Button data → request. A send button (<c>send:</c>) is not a request: it only asks for the confirmation
    /// (<see cref="NotificationFormatter.ConfirmSend"/>), and its <c>ok:</c> press sends.
    /// </summary>
    public static Maybe<OwnerRequest> ParseCallback(long chatId, long userId, string data) =>
        Send(data, NotificationFormatter.ConfirmSendPrefix + "tg:", MessageChannel.Telegram, chatId, userId)
            .Or(() => Send(data, NotificationFormatter.ConfirmSendPrefix + "app:", MessageChannel.App, chatId, userId))
            .Or(() => Send(data, NotificationFormatter.ConfirmSendPrefix + "bot:", MessageChannel.BotChat, chatId, userId))
            .Or(() => TryId(data, NotificationFormatter.ConfirmSendPrefix).Map(id => (OwnerRequest)new DraftDecisionRequest(chatId, userId, id, Approve: true)))
            .Or(() => TryId(data, NotificationFormatter.SkipPrefix).Map(id => (OwnerRequest)new DraftDecisionRequest(chatId, userId, id, Approve: false)))
            .Or(() => TryId(data, NotificationFormatter.LookUpPrefix).Map(id => (OwnerRequest)new LookUpContactsRequest(chatId, userId, id)))
            .Or(() => TryId(data, NotificationFormatter.ForgetPrefix).Map(id => (OwnerRequest)new ForgetRequest(chatId, userId, id)))
            .Or(() => TryId(data, NotificationFormatter.LikeBackPrefix).Map(id => (OwnerRequest)new LikeBackDecisionRequest(chatId, userId, id, Approve: true)))
            .Or(() => TryId(data, NotificationFormatter.NoLikeBackPrefix).Map(id => (OwnerRequest)new LikeBackDecisionRequest(chatId, userId, id, Approve: false)))
            .Or(() => TryId(data, NotificationFormatter.EditPrefix).Map(id => (OwnerRequest)new EditDraftRequest(chatId, userId, id, DraftEditKind.OwnText)))
            .Or(() => TryId(data, NotificationFormatter.RewritePrefix).Map(id => (OwnerRequest)new EditDraftRequest(chatId, userId, id, DraftEditKind.Hint)));

    private async Task<Maybe<OwnerRequest>> ParseCallbackAsync(
        ITelegramBotClient client, CallbackQuery query, Message message, string data, CancellationToken ct)
    {
        var confirm = NotificationFormatter.ConfirmSend(data);
        try
        {
            await client.AnswerCallbackQuery(query.Id, confirm.HasValue ? "Подтвердите отправку" : "Принято", cancellationToken: ct);
            if (confirm.HasValue)
            {
                await client.EditMessageReplyMarkup(
                    message.Chat.Id, message.MessageId, WithConfirmation(message.ReplyMarkup, confirm.GetValueOrThrow()), cancellationToken: ct);
            }
            else if (!KeepsButtons(data))
            {
                await client.EditMessageReplyMarkup(message.Chat.Id, message.MessageId, replyMarkup: null, cancellationToken: ct);
            }
        }
        catch (RequestException ex)
        {
            logger.LogDebug(ex, "Could not acknowledge button press");
        }

        return confirm.HasValue ? Maybe<OwnerRequest>.None : ParseCallback(message.Chat.Id, query.From.Id, data);
    }

    /// <summary>
    /// A decision takes the buttons off its card, so it cannot be pressed twice. An edit does not: the waiting edit lives
    /// in memory, and a restart or a cancel must not leave the card without buttons.
    /// </summary>
    public static bool KeepsButtons(string data) =>
        data.StartsWith(NotificationFormatter.EditPrefix, StringComparison.Ordinal)
        || data.StartsWith(NotificationFormatter.RewritePrefix, StringComparison.Ordinal);

    /// <summary>The confirmation on top, the card's own buttons below it, so another choice is still one tap away.</summary>
    private static InlineKeyboardMarkup WithConfirmation(InlineKeyboardMarkup? current, InlineAction confirm) =>
        new(new[] { new[] { InlineKeyboardButton.WithCallbackData(confirm.Text, confirm.Data) } }
            .Concat((current?.InlineKeyboard ?? [])
                .Select(row => row.Where(b => b.CallbackData?.StartsWith(NotificationFormatter.ConfirmSendPrefix, StringComparison.Ordinal) != true).ToArray())
                .Where(row => row.Length > 0)));

    private static Maybe<OwnerRequest> Send(string data, string prefix, MessageChannel channel, long chatId, long userId) =>
        TryId(data, prefix).Map(id => (OwnerRequest)new DraftDecisionRequest(chatId, userId, id, Approve: true, channel));

    private static Maybe<Guid> TryId(string data, string prefix) =>
        data.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParseExact(data[prefix.Length..], "N", out var id)
            ? id
            : Maybe<Guid>.None;
}
