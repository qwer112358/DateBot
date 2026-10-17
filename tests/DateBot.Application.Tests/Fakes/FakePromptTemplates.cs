using CSharpFunctionalExtensions;
using DateBot.Application.Ports.Services;
using DateBot.Application.Replies.Models;
using DateBot.Application.Tests.Support;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Tests.Fakes;

internal sealed class FakePromptTemplates : IPromptTemplates
{
    /// <summary>What the file holds now; a failure stands for a file broken by an edit.</summary>
    public Result<PromptTemplates, Error> Current { get; set; } = ShippedPrompts.Templates;

    public Task<Result<PromptTemplates, Error>> LoadAsync(CancellationToken ct) => Task.FromResult(Current);
}
