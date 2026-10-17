using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.Processing.Enums;
using DateBot.Application.Processing.Options;
using DateBot.Application.Processing.Routes;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Domain.Shared.ValueObjects;

namespace DateBot.Application.Processing.Services;

/// <summary>Pure: which channels can reach the person now, and the route for the chosen one.</summary>
public static class MessageRouter
{
    /// <summary>
    /// Telegram and the in-app chat need a match (and the username / app id); the bot chat needs the notification
    /// the text answers. Order follows <see cref="ProcessingOptions.MutualRoute"/>.
    /// </summary>
    public static ImmutableArray<MessageChannel> Available(Match match, Maybe<BotMessageId> anchor, ProcessingOptions options) =>
        Available(match.Profile, match.IsMutual, anchor, options);

    /// <inheritdoc cref="Available(Match, Maybe{BotMessageId}, ProcessingOptions)"/>
    public static ImmutableArray<MessageChannel> Available(Profile profile, bool mutual, Maybe<BotMessageId> anchor, ProcessingOptions options)
    {
        var available = new List<MessageChannel>(3);
        if (mutual && profile.Username.HasValue)
        {
            available.Add(MessageChannel.Telegram);
        }

        if (mutual && profile.AppId.HasValue && options.MiniApp.Enabled)
        {
            available.Add(MessageChannel.App);
        }

        if (anchor.HasValue)
        {
            available.Add(MessageChannel.BotChat);
        }

        return
        [
            .. available
                .Where(options.IsEnabled)
                .OrderBy(channel => Array.IndexOf(Preference(options.MutualRoute), channel)),
        ];
    }

    /// <summary>The first available channel in the configured order (Auto mode).</summary>
    public static Result<MessageRoute, Error> Choose(Match match, Maybe<BotMessageId> anchor, ProcessingOptions options) =>
        Available(match, anchor, options)
            .TryFirst()
            .ToResult(MatchErrors.ChannelUnavailable(MessageChannel.BotChat, "нет ни одного канала: нужен мэтч или уведомление бота"))
            .Bind(channel => Route(channel, match, anchor, options));

    public static Result<MessageRoute, Error> Route(MessageChannel channel, Match match, Maybe<BotMessageId> anchor, ProcessingOptions options) =>
        Available(match, anchor, options).Contains(channel)
            ? channel switch
            {
                MessageChannel.Telegram => new DirectRoute(match.Profile.Username.GetValueOrThrow()),
                MessageChannel.App => new AppChatRoute(match.Profile.AppId.GetValueOrThrow(), ClientKey(match)),
                _ => new ViaBotRoute(anchor.GetValueOrThrow(), [.. options.MessageButtons]),
            }
            : Result.Failure<MessageRoute, Error>(MatchErrors.ChannelUnavailable(channel, Reason(channel, match, options)));

    /// <summary>At most one message per person, so the match id is a stable idempotency key for it.</summary>
    public static string ClientKey(Match match) => match.Id.Value.ToString("D");

    private static MessageChannel[] Preference(MessageRouteKind kind) => kind switch
    {
        MessageRouteKind.AppChat => [MessageChannel.App, MessageChannel.Telegram, MessageChannel.BotChat],
        MessageRouteKind.ViaBot => [MessageChannel.BotChat, MessageChannel.Telegram, MessageChannel.App],
        _ => [MessageChannel.Telegram, MessageChannel.App, MessageChannel.BotChat],
    };

    private static string Reason(MessageChannel channel, Match match, ProcessingOptions options) => channel switch
    {
        _ when !options.IsEnabled(channel) => "канал выключен в Processing:Channels",
        _ when !match.IsMutual && channel != MessageChannel.BotChat => "взаимной симпатии ещё нет",
        MessageChannel.Telegram => "у человека нет username в Telegram",
        MessageChannel.App => "нет id анкеты в приложении или работа с Mini App выключена",
        _ => "нет уведомления бота, на которое можно ответить",
    };
}
