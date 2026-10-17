using CSharpFunctionalExtensions;
using DateBot.Application.Ports.Services;
using DateBot.Application.Replies.Models;
using DateBot.Application.Tests.Support;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Tests.Fakes;

internal sealed class FakeLlmSettings : ILlmSettings
{
    /// <summary>What llm.json holds now; change it to stand for an edit.</summary>
    public LlmSettings Current { get; set; } = TestOptions.Llm();

    public Task<Result<LlmSettings, Error>> LoadAsync(CancellationToken ct) => Task.FromResult(Result.Success<LlmSettings, Error>(Current));
}
