using CSharpFunctionalExtensions;
using DateBot.Application.Ports.Services;
using DateBot.Application.Replies.Models;
using DateBot.Application.Telegram.Models;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Tests.Fakes;

internal sealed class FakeReplyGenerator : IReplyGenerator
{
    public const string GoodReply = "Привет, Анна! Вижу, ты любишь горы. Какой поход запомнился тебе больше всего?";

    public Func<ReplyPrompt, Result<string, Error>> Respond { get; set; } = _ => Result.Success<string, Error>(GoodReply);

    public List<(ReplyPrompt Prompt, IReadOnlyList<ProfilePhoto> Photos, LlmSettings Settings)> Calls { get; } = [];

    public Task<Result<string, Error>> GenerateAsync(ReplyPrompt prompt, IReadOnlyList<ProfilePhoto> photos, LlmSettings settings, CancellationToken ct)
    {
        Calls.Add((prompt, photos, settings));
        return Task.FromResult(Respond(prompt));
    }
}
