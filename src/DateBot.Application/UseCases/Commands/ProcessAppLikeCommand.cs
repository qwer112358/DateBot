using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.MiniApp.Models;
using DateBot.Application.UseCases.Enums;

namespace DateBot.Application.UseCases.Commands;

/// <summary>One incoming like from the app, end to end, in its own scope.</summary>
public sealed record ProcessAppLikeCommand(AppLike Like) : ICommand<ProcessOutcome>;
