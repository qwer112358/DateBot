using DateBot.Application.Abstractions;
using DateBot.Application.MiniApp;

namespace DateBot.Application.UseCases;

/// <summary>One incoming like from the app, end to end, in its own scope.</summary>
public sealed record ProcessAppLikeCommand(AppLike Like) : ICommand<ProcessOutcome>;
