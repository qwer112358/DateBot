using CSharpFunctionalExtensions;
using DateBot.Application.Ports;
using DateBot.Application.Replies;
using DateBot.Application.Telegram;
using DateBot.Domain.Common;

namespace DateBot.Application.Tests;

internal sealed class FakeReplyGenerator : IReplyGenerator
{
    public const string GoodReply = "Привет, Анна! Вижу, ты любишь горы. Какой поход запомнился тебе больше всего?";

    public Func<ReplyPrompt, Result<string, Error>> Respond { get; set; } = _ => Result.Success<string, Error>(GoodReply);

    public List<(ReplyPrompt Prompt, Maybe<ProfilePhoto> Photo)> Calls { get; } = [];

    public Task<Result<string, Error>> GenerateAsync(ReplyPrompt prompt, Maybe<ProfilePhoto> photo, CancellationToken ct)
    {
        Calls.Add((prompt, photo));
        return Task.FromResult(Respond(prompt));
    }
}
