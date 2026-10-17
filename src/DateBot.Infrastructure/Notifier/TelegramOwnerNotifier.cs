using CSharpFunctionalExtensions;
using DateBot.Application.Notifications;
using DateBot.Application.Ports;
using DateBot.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DateBot.Infrastructure.Notifier;

/// <summary>
/// Sends notifications to your chat with the notifier bot. Push notifications work because the message
/// comes from a bot (messages to Saved Messages do not ring). Quiet hours send silently.
/// </summary>
internal sealed class TelegramOwnerNotifier(
    NotifierBot bot,
    IServiceScopeFactory scopes,
    IPhotoStore photos,
    IClock clock,
    IOptions<NotifierOptions> options,
    ILogger<TelegramOwnerNotifier> logger)
    : IOwnerNotifier
{
    private const int CaptionLimit = 1024;
    private const int TextLimit = 4000;
    private const int ButtonsPerRow = 2;

    public async Task<UnitResult<Error>> NotifyAsync(OwnerNotification notification, CancellationToken ct)
    {
        if (options.Value.Verbosity == NotificationVerbosity.ErrorsOnly && notification.Importance == NotificationImportance.Info)
        {
            return UnitResult.Success<Error>();
        }

        var formatted = NotificationFormatter.Format(notification);
        var chat = await OwnerChatAsync(ct);
        if (bot.Client.HasNoValue || chat.HasNoValue)
        {
            logger.LogInformation("Notification (not sent: notifier bot or chat not set up): {Text}", formatted.Html);
            return UnitResult.Success<Error>();
        }

        try
        {
            await SendAsync(bot.Client.GetValueOrThrow(), chat.GetValueOrThrow(), formatted, IsQuietTime(), ct);
            return UnitResult.Success<Error>();
        }
        catch (Exception ex) when (ex is RequestException or HttpRequestException)
        {
            logger.LogWarning(ex, "Notification not delivered: {Text}", formatted.Html);
            return UnitResult.Failure(Error.External("notifier.unavailable", ex.Message));
        }
    }

    private async Task SendAsync(ITelegramBotClient client, long chatId, FormattedNotification formatted, bool silent, CancellationToken ct)
    {
        var markup = formatted.Buttons.IsDefaultOrEmpty
            ? null
            : new InlineKeyboardMarkup(formatted.Buttons
                .Chunk(ButtonsPerRow)
                .Select(row => row.Select(b => InlineKeyboardButton.WithCallbackData(b.Text, b.Data))));
        var photo = await LoadPhotoAsync(formatted, ct);

        if (photo.HasValue && formatted.Html.Length <= CaptionLimit)
        {
            using var stream = new MemoryStream(photo.GetValueOrThrow());
            await client.SendPhoto(chatId, InputFile.FromStream(stream, "photo.jpg"), formatted.Html, ParseMode.Html,
                replyMarkup: markup, disableNotification: silent, cancellationToken: ct);
            return;
        }

        if (photo.HasValue)
        {
            using var stream = new MemoryStream(photo.GetValueOrThrow());
            await client.SendPhoto(chatId, InputFile.FromStream(stream, "photo.jpg"), disableNotification: silent, cancellationToken: ct);
        }

        var html = formatted.Html.Length <= TextLimit ? formatted.Html : formatted.Html[..TextLimit];
        await client.SendMessage(chatId, html, ParseMode.Html, replyMarkup: markup, disableNotification: silent, cancellationToken: ct);
    }

    private async Task<Maybe<byte[]>> LoadPhotoAsync(FormattedNotification formatted, CancellationToken ct)
    {
        if (formatted.Photo.HasNoValue)
        {
            return Maybe<byte[]>.None;
        }

        var loaded = await photos.LoadAsync(formatted.Photo.GetValueOrThrow(), ct);
        return loaded.IsSuccess && loaded.Value.HasValue
            ? loaded.Value.GetValueOrThrow().Content.ToArray()
            : Maybe<byte[]>.None;
    }

    private async Task<Maybe<long>> OwnerChatAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var state = await scope.ServiceProvider.GetRequiredService<IControlStateStore>().GetAsync(ct);
        return state.IsSuccess ? state.Value.OwnerChatId : Maybe<long>.None;
    }

    private bool IsQuietTime()
    {
        var settings = options.Value;
        if (!settings.QuietHoursEnabled || !TimeZoneInfo.TryFindSystemTimeZoneById(settings.TimeZone, out var zone))
        {
            return false;
        }

        var local = TimeZoneInfo.ConvertTime(clock.UtcNow, zone).TimeOfDay;
        return settings.QuietFrom <= settings.QuietTo
            ? local >= settings.QuietFrom && local < settings.QuietTo
            : local >= settings.QuietFrom || local < settings.QuietTo;
    }
}
