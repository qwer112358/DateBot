using CSharpFunctionalExtensions;
using DateBot.Infrastructure.Notifier.Options;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace DateBot.Infrastructure.Notifier.Services;

/// <summary>The Bot API client, present only when a token is configured.</summary>
public sealed class NotifierBot(IOptions<NotifierOptions> options)
{
    private readonly Lazy<Maybe<ITelegramBotClient>> _client = new(() =>
        string.IsNullOrWhiteSpace(options.Value.BotToken)
            ? Maybe<ITelegramBotClient>.None
            : Maybe.From<ITelegramBotClient>(new TelegramBotClient(options.Value.BotToken)));

    public Maybe<ITelegramBotClient> Client => _client.Value;
}
