using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.Ports;
using DateBot.Application.Telegram;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;

namespace DateBot.Application.Tests;

internal sealed class FakeTashdateClient : ITashdateClient
{
    public List<RawBotMessage> History { get; } = [];

    public List<(long MessageId, ImmutableArray<string> Labels)> Presses { get; } = [];

    public List<string> SentToBot { get; } = [];

    public List<(string Username, string Text)> SentToUsers { get; } = [];

    public Queue<Error> PressFailures { get; } = new();

    public Queue<Error> SendFailures { get; } = new();

    public Maybe<ProfilePhoto> Photo { get; set; } = Maybe<ProfilePhoto>.None;

    public int Downloads { get; private set; }

    public int Sends => SentToBot.Count + SentToUsers.Count;

    public Task<Result<long, Error>> ConnectAsync(CancellationToken ct) => Task.FromResult(Result.Success<long, Error>(42));

    public Task<Result<long, Error>> GetPositionBeforeAsync(DateTimeOffset since, CancellationToken ct) =>
        Task.FromResult(Result.Success<long, Error>(History.Where(m => m.SentAt < since).Select(m => m.Id).DefaultIfEmpty(0).Max()));

    public Task<Result<ImmutableArray<RawBotMessage>, Error>> GetHistoryAsync(long afterMessageId, int limit, CancellationToken ct) =>
        Task.FromResult(Result.Success<ImmutableArray<RawBotMessage>, Error>(
            [.. History.Where(m => m.Id > afterMessageId).OrderBy(m => m.Id).Take(limit)]));

    public Task<UnitResult<Error>> WaitForActivityAsync(TimeSpan timeout, CancellationToken ct) =>
        Task.FromResult(UnitResult.Success<Error>());

    public Task<UnitResult<Error>> PressButtonAsync(BotMessageId messageId, ImmutableArray<string> labels, CancellationToken ct)
    {
        if (PressFailures.TryDequeue(out var error))
        {
            return Task.FromResult(UnitResult.Failure(error));
        }

        Presses.Add((messageId.Value, labels));
        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<UnitResult<Error>> SendToBotAsync(ReplyText text, CancellationToken ct)
    {
        if (SendFailures.TryDequeue(out var error))
        {
            return Task.FromResult(UnitResult.Failure(error));
        }

        SentToBot.Add(text.Value);
        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<UnitResult<Error>> SendToUserAsync(ContactUsername username, ReplyText text, CancellationToken ct)
    {
        if (SendFailures.TryDequeue(out var error))
        {
            return Task.FromResult(UnitResult.Failure(error));
        }

        SentToUsers.Add((username.Value, text.Value));
        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<Result<Maybe<ProfilePhoto>, Error>> DownloadPhotoAsync(BotMessageId messageId, CancellationToken ct)
    {
        Downloads++;
        return Task.FromResult(Result.Success<Maybe<ProfilePhoto>, Error>(Photo));
    }
}
