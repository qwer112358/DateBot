using DateBot.Application.Abstractions.Messaging;

namespace DateBot.Application.UseCases.Commands;

/// <summary>After login: remember which Telegram user owns the service.</summary>
public sealed record RememberOwnerAccountCommand(long UserId) : ICommand<Unit>;
