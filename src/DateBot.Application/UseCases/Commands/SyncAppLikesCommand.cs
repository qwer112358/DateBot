using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.UseCases.Models;

namespace DateBot.Application.UseCases.Commands;

/// <summary>Answer the likes waiting in the app's "Likes" tab.</summary>
public sealed record SyncAppLikesCommand : ICommand<AppSyncReport>;
