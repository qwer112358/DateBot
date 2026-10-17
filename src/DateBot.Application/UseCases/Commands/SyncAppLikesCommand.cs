using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.UseCases.Models;

namespace DateBot.Application.UseCases.Commands;

/// <summary>Answer the likes waiting in the app's "Likes" tab.</summary>
/// <param name="ResendWaiting">/likes: cards still waiting for your answer come again, with their album.</param>
public sealed record SyncAppLikesCommand(bool ResendWaiting = false) : ICommand<AppSyncReport>;
