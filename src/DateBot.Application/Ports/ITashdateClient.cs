using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.Telegram;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;

namespace DateBot.Application.Ports;

/// <summary>Your Telegram account (userbot) talking to the Tashdate bot.</summary>
public interface ITashdateClient
{
    /// <summary>Logs in (code from Telegram on the first run) and finds the bot chat. Returns your Telegram user id.</summary>
    Task<Result<long, Error>> ConnectAsync(CancellationToken ct);

    /// <summary>Id of the newest message in the bot chat sent before <paramref name="since"/>; 0 when there is none.</summary>
    Task<Result<long, Error>> GetPositionBeforeAsync(DateTimeOffset since, CancellationToken ct);

    /// <summary>Incoming bot messages with id greater than <paramref name="afterMessageId"/>, oldest first.</summary>
    Task<Result<ImmutableArray<RawBotMessage>, Error>> GetHistoryAsync(long afterMessageId, int limit, CancellationToken ct);

    /// <summary>Completes when the bot writes something new or when <paramref name="timeout"/> passes.</summary>
    Task<UnitResult<Error>> WaitForActivityAsync(TimeSpan timeout, CancellationToken ct);

    /// <summary>Presses the first inline button of the message whose text contains one of <paramref name="labels"/>.</summary>
    Task<UnitResult<Error>> PressButtonAsync(BotMessageId messageId, ImmutableArray<string> labels, CancellationToken ct);

    Task<UnitResult<Error>> SendToBotAsync(ReplyText text, CancellationToken ct);

    Task<UnitResult<Error>> SendToUserAsync(ContactUsername username, ReplyText text, CancellationToken ct);

    /// <summary>The largest size of the photo attached to the bot message, if any.</summary>
    Task<Result<Maybe<ProfilePhoto>, Error>> DownloadPhotoAsync(BotMessageId messageId, CancellationToken ct);
}
