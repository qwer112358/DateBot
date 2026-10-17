using System.Collections.Immutable;
using System.Globalization;
using System.Net.Sockets;
using System.Threading.Channels;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TashdateBot.Application.Ports;
using TashdateBot.Application.Telegram;
using TashdateBot.Domain.Common;
using TashdateBot.Domain.Matches;
using TashdateBot.Domain.Shared;
using TashdateBot.Infrastructure.Platform;
using TL;
using WTelegram;
using Error = TashdateBot.Domain.Common.Error;

namespace TashdateBot.Infrastructure.Telegram;

/// <summary>
/// Userbot over MTProto (WTelegramClient). Library exceptions never leave this class:
/// they become <see cref="Result"/> failures (FLOOD_WAIT → RateLimited, network → retryable).
/// </summary>
internal sealed class WTelegramTashdateClient : ITashdateClient, IAsyncDisposable
{
    private const int HistoryPageSize = 100;

    private readonly TelegramOptions _options;
    private readonly StorageOptions _storage;
    private readonly ILoginPrompt _prompt;
    private readonly ILogger<WTelegramTashdateClient> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Channel<bool> _activity = System.Threading.Channels.Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private Client? _client;
    private User? _bot;
    private volatile bool _broken;

    public WTelegramTashdateClient(
        IOptions<TelegramOptions> options,
        IOptions<StorageOptions> storage,
        ILoginPrompt prompt,
        ILogger<WTelegramTashdateClient> logger)
    {
        _options = options.Value;
        _storage = storage.Value;
        _prompt = prompt;
        _logger = logger;
        Helpers.Log = (level, message) => _logger.Log(level >= 3 ? LogLevel.Warning : LogLevel.Debug, "WTelegram: {Message}", message);
    }

    public Task<Result<long, Error>> ConnectAsync(CancellationToken ct) =>
        Guard(async () =>
        {
            var (client, bot) = await EnsureConnectedAsync(ct);
            _logger.LogInformation("Logged in as {User}; Tashdate bot is @{Bot} ({BotId})", client.User, bot.MainUsername, bot.id);
            return Result.Success<long, Error>(client.UserId);
        });

    public Task<Result<long, Error>> GetPositionBeforeAsync(DateTimeOffset since, CancellationToken ct) =>
        Guard(async () =>
        {
            var (client, bot) = await EnsureConnectedAsync(ct);
            var page = await client.Messages_GetHistory(bot, offset_date: since.UtcDateTime, limit: 1);
            var messages = page.Messages ?? [];
            return Result.Success<long, Error>(messages.Length == 0 ? 0 : messages.Max(m => m.ID));
        });

    public Task<Result<ImmutableArray<RawBotMessage>, Error>> GetHistoryAsync(long afterMessageId, int limit, CancellationToken ct) =>
        Guard(async () =>
        {
            var (client, bot) = await EnsureConnectedAsync(ct);
            var incoming = new List<Message>();
            var offset = 0;

            // Page backwards from the newest message down to the cursor: always complete, never skips a message.
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var page = await client.Messages_GetHistory(bot, offset_id: offset, limit: HistoryPageSize, min_id: (int)afterMessageId);
                var messages = page.Messages ?? [];
                incoming.AddRange(messages.OfType<Message>().Where(m => !m.flags.HasFlag(Message.Flags.out_)));
                if (messages.Length < HistoryPageSize)
                {
                    break;
                }

                offset = messages.Min(m => m.ID);
            }

            return Result.Success<ImmutableArray<RawBotMessage>, Error>(
                [.. incoming.Where(m => m.id > afterMessageId).OrderBy(m => m.id).Take(limit).Select(ToRaw)]);
        });

    public async Task<UnitResult<Error>> WaitForActivityAsync(TimeSpan timeout, CancellationToken ct)
    {
        var connected = await Guard(async () =>
        {
            await EnsureConnectedAsync(ct);
            return Result.Success<bool, Error>(true);
        });
        if (connected.IsFailure)
        {
            return UnitResult.Failure(connected.Error);
        }

        using var timer = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timer.CancelAfter(timeout);
        try
        {
            await _activity.Reader.ReadAsync(timer.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout: the caller polls the history anyway.
        }

        return UnitResult.Success<Error>();
    }

    public Task<UnitResult<Error>> PressButtonAsync(BotMessageId messageId, ImmutableArray<string> labels, CancellationToken ct) =>
        GuardUnit(async () =>
        {
            var (client, bot) = await EnsureConnectedAsync(ct);
            var found = await FindMessageAsync(client, messageId);
            if (found.HasNoValue)
            {
                return UnitResult.Failure(TelegramErrors.MessageNotFound(messageId));
            }

            var message = found.GetValueOrThrow();
            var keyboardLabel = message.reply_markup is ReplyKeyboardMarkup keyboard ? KeyboardLabel(keyboard, labels) : Maybe<string>.None;
            return message.reply_markup switch
            {
                ReplyInlineMarkup inline => await PressInlineAsync(client, bot, message, inline, labels),
                ReplyKeyboardMarkup when keyboardLabel.HasValue => await PressKeyboardAsync(client, bot, message, keyboardLabel.GetValueOrThrow()),
                _ => UnitResult.Failure(TelegramErrors.ButtonNotFound(messageId, labels)),
            };
        });

    public Task<UnitResult<Error>> SendToBotAsync(ReplyText text, CancellationToken ct) =>
        GuardUnit(async () =>
        {
            var (client, bot) = await EnsureConnectedAsync(ct);
            await client.SendMessageAsync(bot, text.Value);
            return UnitResult.Success<Error>();
        });

    public Task<UnitResult<Error>> SendToUserAsync(ContactUsername username, ReplyText text, CancellationToken ct) =>
        GuardUnit(async () =>
        {
            var (client, _) = await EnsureConnectedAsync(ct);
            Contacts_ResolvedPeer resolved;
            try
            {
                resolved = await client.Contacts_ResolveUsername(username.Value);
            }
            catch (RpcException ex) when (ex.Message is "USERNAME_NOT_OCCUPIED" or "USERNAME_INVALID")
            {
                return UnitResult.Failure(TelegramErrors.UserNotFound(username));
            }

            if (resolved.User is not { } user)
            {
                return UnitResult.Failure(TelegramErrors.UserNotFound(username));
            }

            await client.SendMessageAsync(user, text.Value);
            return UnitResult.Success<Error>();
        });

    public Task<Result<Maybe<ProfilePhoto>, Error>> DownloadPhotoAsync(BotMessageId messageId, CancellationToken ct) =>
        Guard(async () =>
        {
            var (client, _) = await EnsureConnectedAsync(ct);
            var found = await FindMessageAsync(client, messageId);
            if (found.HasNoValue || found.GetValueOrThrow().media is not MessageMediaPhoto { photo: Photo photo })
            {
                return Result.Success<Maybe<ProfilePhoto>, Error>(Maybe<ProfilePhoto>.None);
            }

            using var stream = new MemoryStream();
            var type = await client.DownloadFileAsync(photo, stream);
            var mediaType = type switch
            {
                Storage_FileType.png => "image/png",
                Storage_FileType.webp => "image/webp",
                Storage_FileType.gif => "image/gif",
                _ => "image/jpeg",
            };
            return Result.Success<Maybe<ProfilePhoto>, Error>(Maybe.From(new ProfilePhoto(stream.ToArray(), mediaType)));
        });

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        _gate.Dispose();
    }

    private async Task<(Client Client, User Bot)> EnsureConnectedAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_broken && _client is not null)
            {
                _logger.LogWarning("Reconnecting to Telegram");
                await _client.DisposeAsync();
                _client = null;
                _bot = null;
            }

            _broken = false;
            if (_client is not null && _bot is not null)
            {
                return (_client, _bot);
            }

            _client ??= CreateClient();
            await _client.LoginUserIfNeeded();
            var resolved = await _client.Contacts_ResolveUsername(_options.TashdateBot.TrimStart('@'));
            _bot = resolved.User ?? throw new BotNotFoundException();
            return (_client, _bot);
        }
        finally
        {
            _gate.Release();
        }
    }

    private Client CreateClient()
    {
        var dataDirectory = _storage.ResolveDataDirectory();
        Directory.CreateDirectory(dataDirectory);
        var client = new Client(what => what switch
        {
            "api_id" => _options.ApiId.ToString(CultureInfo.InvariantCulture),
            "api_hash" => _options.ApiHash,
            "phone_number" => _options.PhoneNumber,
            "session_pathname" => Path.Combine(dataDirectory, _options.SessionFile),
            "verification_code" => _prompt.Ask("Код подтверждения из Telegram: "),
            "password" => _options.Password.Length > 0 ? _options.Password : _prompt.AskSecret("Пароль двухэтапной проверки: "),
            _ => null!,
        });
        client.OnUpdates += OnUpdatesAsync;
        client.OnOther += OnOtherAsync;
        return client;
    }

    private Task OnUpdatesAsync(UpdatesBase updates)
    {
        if (_bot is { } bot && updates.UpdateList.Any(u => IsFromBot(u, bot.id)))
        {
            _activity.Writer.TryWrite(true);
        }

        return Task.CompletedTask;
    }

    private static bool IsFromBot(Update update, long botId) =>
        update is UpdateNewMessage { message: Message { peer_id: PeerUser { user_id: var userId } } message }
        && userId == botId
        && !message.flags.HasFlag(Message.Flags.out_);

    private Task OnOtherAsync(IObject notification)
    {
        if (notification is ReactorError error)
        {
            _logger.LogWarning(error.Exception, "Telegram connection lost");
            _broken = true;
        }

        return Task.CompletedTask;
    }

    private async Task<UnitResult<Error>> PressInlineAsync(
        Client client, User bot, Message message, ReplyInlineMarkup inline, ImmutableArray<string> labels)
    {
        var button = inline.rows
            .SelectMany(row => row.buttons)
            .FirstOrDefault(b => b.type is InlineButtonTypeCallback && Matches(b.text, labels));
        if (button?.type is not InlineButtonTypeCallback callback)
        {
            return UnitResult.Failure(TelegramErrors.ButtonNotFound(BotMessageId.Create(message.id).Value, labels));
        }

        try
        {
            await client.Messages_GetBotCallbackAnswer(bot, message.id, callback.data);
        }
        catch (RpcException ex) when (ex.Message == "BOT_RESPONSE_TIMEOUT")
        {
            // The press was delivered; the bot just did not answer the callback in time.
            _logger.LogDebug("Bot did not answer the callback for message {MessageId} in time", message.id);
        }

        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// A reply keyboard acts on whatever the bot shows now, so pressing it is safe only for the latest bot message;
    /// otherwise the like would go to a different person.
    /// </summary>
    private async Task<UnitResult<Error>> PressKeyboardAsync(Client client, User bot, Message message, string label)
    {
        var latest = await client.Messages_GetHistory(bot, limit: 10);
        var latestIncoming = (latest.Messages ?? []).OfType<Message>().FirstOrDefault(m => !m.flags.HasFlag(Message.Flags.out_));
        if (latestIncoming?.id != message.id)
        {
            return UnitResult.Failure(Error.Conflict(
                "telegram.stale_keyboard",
                $"Кнопки бота относятся к более новой анкете, чем сообщение {message.id}; нажатие пропущено, чтобы не лайкнуть не того человека"));
        }

        await client.SendMessageAsync(bot, label);
        return UnitResult.Success<Error>();
    }

    private static Maybe<string> KeyboardLabel(ReplyKeyboardMarkup keyboard, ImmutableArray<string> labels) =>
        keyboard.rows.SelectMany(row => row.buttons).Select(b => b.text).TryFirst(text => Matches(text, labels));

    private static bool Matches(string text, ImmutableArray<string> labels) =>
        labels.Any(label => text.Contains(label, StringComparison.OrdinalIgnoreCase));

    private static async Task<Maybe<Message>> FindMessageAsync(Client client, BotMessageId id)
    {
        var result = await client.Messages_GetMessages(new InputMessageID { id = (int)id.Value });
        return (result.Messages ?? []).OfType<Message>().TryFirst(m => m.id == id.Value);
    }

    private static RawBotMessage ToRaw(Message message) => new(
        message.id,
        new DateTimeOffset(DateTime.SpecifyKind(message.date, DateTimeKind.Utc)),
        message.message ?? string.Empty,
        Buttons(message.reply_markup),
        message.media is MessageMediaPhoto { photo: Photo photo }
            ? Maybe.From("photo:" + photo.id.ToString(CultureInfo.InvariantCulture))
            : Maybe<string>.None);

    private static ImmutableArray<BotButton> Buttons(ReplyMarkup? markup) => markup switch
    {
        ReplyInlineMarkup inline => [.. inline.rows.SelectMany((row, r) => row.buttons.Select((b, c) => new BotButton(b.text, r, c)))],
        ReplyKeyboardMarkup keyboard => [.. keyboard.rows.SelectMany((row, r) => row.buttons.Select((b, c) => new BotButton(b.text, r, c)))],
        _ => [],
    };

    private Task<UnitResult<Error>> GuardUnit(Func<Task<UnitResult<Error>>> action) =>
        Guard(async () =>
        {
            var result = await action();
            return result.IsSuccess ? Result.Success<bool, Error>(true) : Result.Failure<bool, Error>(result.Error);
        }).Bind(_ => UnitResult.Success<Error>());

    private async Task<Result<T, Error>> Guard<T>(Func<Task<Result<T, Error>>> action)
    {
        try
        {
            return await action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BotNotFoundException)
        {
            return Result.Failure<T, Error>(TelegramErrors.BotNotFound);
        }
        catch (RpcException ex) when (ex.Code == 420)
        {
            return Result.Failure<T, Error>(TelegramErrors.FloodWait(Math.Max(ex.X, 1)));
        }
        catch (RpcException ex) when (ex.Message == "USERNAME_NOT_OCCUPIED")
        {
            return Result.Failure<T, Error>(TelegramErrors.BotNotFound);
        }
        catch (RpcException ex)
        {
            return Result.Failure<T, Error>(TelegramErrors.Rpc(ex.Code, ex.Message));
        }
        catch (Exception ex) when (ex is IOException or SocketException or WTException or TimeoutException or ObjectDisposedException)
        {
            _broken = true;
            return Result.Failure<T, Error>(TelegramErrors.Unavailable(ex.Message));
        }
    }

    private sealed class BotNotFoundException : Exception;
}
