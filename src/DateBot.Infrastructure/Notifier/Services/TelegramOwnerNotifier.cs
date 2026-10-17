using CSharpFunctionalExtensions;
using DateBot.Application.Notifications;
using DateBot.Application.Notifications.Enums;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Infrastructure.Notifier.Enums;
using DateBot.Infrastructure.Notifier.Models;
using DateBot.Infrastructure.Notifier.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DateBot.Infrastructure.Notifier.Services;

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
        var inAlbum = await SendAlbumAsync(client, chatId, formatted, silent, ct);
        var photo = formatted.Photo.HasValue && !inAlbum ? await LoadPhotoAsync(formatted.Photo.GetValueOrThrow(), ct) : Maybe<byte[]>.None;

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

    /// <summary>
    /// All the profile's photos, main first, as one album; the card then follows as text right under it, because
    /// Telegram albums cannot carry buttons. Photos missing from the store are skipped; with fewer than two left there
    /// is no album, and the card keeps its photo.
    /// </summary>
    /// <returns>Whether the album, main photo included, went out.</returns>
    private async Task<bool> SendAlbumAsync(ITelegramBotClient client, long chatId, FormattedNotification formatted, bool silent, CancellationToken ct)
    {
        if (formatted.Album.IsDefaultOrEmpty)
        {
            return false;
        }

        var loaded = new List<byte[]>();
        foreach (var id in formatted.Photo.ToList().Concat(formatted.Album))
        {
            var photo = await LoadPhotoAsync(id, ct);
            if (photo.HasValue)
            {
                loaded.Add(photo.GetValueOrThrow());
            }
        }

        if (loaded.Count < 2)
        {
            return false;
        }

        var streams = loaded.Select(bytes => new MemoryStream(bytes)).ToList();
        try
        {
            await client.SendMediaGroup(
                chatId,
                streams.Select((stream, i) => new InputMediaPhoto(InputFile.FromStream(stream, $"photo{i}.jpg"))),
                disableNotification: silent,
                cancellationToken: ct);
            return true;
        }
        finally
        {
            streams.ForEach(stream => stream.Dispose());
        }
    }

    private async Task<Maybe<byte[]>> LoadPhotoAsync(PhotoFileId id, CancellationToken ct)
    {
        var loaded = await photos.LoadAsync(id, ct);
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
