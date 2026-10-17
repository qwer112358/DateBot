using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.MiniApp.Models;
using DateBot.Application.UseCases.Enums;

namespace DateBot.Application.UseCases.Commands;

/// <summary>One incoming like from the app, end to end, in its own scope.</summary>
/// <param name="ResendWaiting">Send the card again if it still waits for your answer.</param>
public sealed record ProcessAppLikeCommand(AppLike Like, bool ResendWaiting = false) : ICommand<ProcessOutcome>;
