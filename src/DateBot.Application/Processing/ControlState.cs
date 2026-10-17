using CSharpFunctionalExtensions;

namespace DateBot.Application.Processing;

/// <param name="Paused">Set by /stop, cleared by /start.</param>
/// <param name="OwnerUserId">Your Telegram user id, taken from the userbot login; only you may command the notifier bot.</param>
/// <param name="OwnerChatId">Chat with the notifier bot, remembered on /start.</param>
public sealed record ControlState(bool Paused, Maybe<long> OwnerUserId, Maybe<long> OwnerChatId)
{
    public static ControlState Initial { get; } = new(false, Maybe<long>.None, Maybe<long>.None);
}
