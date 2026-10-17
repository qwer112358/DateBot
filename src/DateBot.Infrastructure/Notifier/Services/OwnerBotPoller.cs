using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.UseCases.OwnerRequests;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.Enums;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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
            _ => new UnknownRequest(chatId, userId, text),
        };
    }

    public static Maybe<OwnerRequest> ParseCallback(long chatId, long userId, string data) =>
        Send(data, NotificationFormatter.SendToTelegramPrefix, MessageChannel.Telegram, chatId, userId)
            .Or(() => Send(data, NotificationFormatter.SendToAppPrefix, MessageChannel.App, chatId, userId))
            .Or(() => Send(data, NotificationFormatter.SendViaBotPrefix, MessageChannel.BotChat, chatId, userId))
            .Or(() => TryId(data, NotificationFormatter.SendPrefix).Map(id => (OwnerRequest)new DraftDecisionRequest(chatId, userId, id, Approve: true)))
            .Or(() => TryId(data, NotificationFormatter.SkipPrefix).Map(id => (OwnerRequest)new DraftDecisionRequest(chatId, userId, id, Approve: false)))
            .Or(() => TryId(data, NotificationFormatter.LookUpPrefix).Map(id => (OwnerRequest)new LookUpContactsRequest(chatId, userId, id)))
            .Or(() => TryId(data, NotificationFormatter.ForgetPrefix).Map(id => (OwnerRequest)new ForgetRequest(chatId, userId, id)))
            .Or(() => TryId(data, NotificationFormatter.LikeBackPrefix).Map(id => (OwnerRequest)new LikeBackDecisionRequest(chatId, userId, id, Approve: true)))
            .Or(() => TryId(data, NotificationFormatter.NoLikeBackPrefix).Map(id => (OwnerRequest)new LikeBackDecisionRequest(chatId, userId, id, Approve: false)));

    private async Task<Maybe<OwnerRequest>> ParseCallbackAsync(
        ITelegramBotClient client, CallbackQuery query, Message message, string data, CancellationToken ct)
    {
        try
        {
            await client.AnswerCallbackQuery(query.Id, "Принято", cancellationToken: ct);
            await client.EditMessageReplyMarkup(message.Chat.Id, message.MessageId, replyMarkup: null, cancellationToken: ct);
        }
        catch (RequestException ex)
        {
            logger.LogDebug(ex, "Could not acknowledge button press");
        }

        return ParseCallback(message.Chat.Id, query.From.Id, data);
    }

    private static Maybe<OwnerRequest> Send(string data, string prefix, MessageChannel channel, long chatId, long userId) =>
        TryId(data, prefix).Map(id => (OwnerRequest)new DraftDecisionRequest(chatId, userId, id, Approve: true, channel));

    private static Maybe<Guid> TryId(string data, string prefix) =>
        data.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParseExact(data[prefix.Length..], "N", out var id)
            ? id
            : Maybe<Guid>.None;
}
