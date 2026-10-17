using DateBot.Application.Abstractions;

namespace DateBot.Application.UseCases;

/// <summary>After login: remember which Telegram user owns the service.</summary>
public sealed record RememberOwnerAccountCommand(long UserId) : ICommand<Unit>;
