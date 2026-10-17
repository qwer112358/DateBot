using DateBot.Application.Abstractions;

namespace DateBot.Application.UseCases;

/// <summary>Answer the likes waiting in the app's "Likes" tab.</summary>
public sealed record SyncAppLikesCommand : ICommand<AppSyncReport>;
