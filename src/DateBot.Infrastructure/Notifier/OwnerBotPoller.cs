using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.UseCases;
using DateBot.Domain.Common;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DateBot.Infrastructure.Notifier;

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
            _ => new UnknownRequest(chatId, userId, text),
        };
    }

    public static Maybe<OwnerRequest> ParseCallback(long chatId, long userId, string data) =>
        TryId(data, NotificationFormatter.SendPrefix).Map(id => (OwnerRequest)new DraftDecisionRequest(chatId, userId, id, Approve: true))
            .Or(() => TryId(data, NotificationFormatter.SkipPrefix).Map(id => (OwnerRequest)new DraftDecisionRequest(chatId, userId, id, Approve: false)))
            .Or(() => TryId(data, NotificationFormatter.ForgetPrefix).Map(id => (OwnerRequest)new ForgetRequest(chatId, userId, id)));

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

    private static Maybe<Guid> TryId(string data, string prefix) =>
        data.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParseExact(data[prefix.Length..], "N", out var id)
            ? id
            : Maybe<Guid>.None;
}
