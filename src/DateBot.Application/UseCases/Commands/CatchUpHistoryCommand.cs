using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.UseCases.Models;

namespace DateBot.Application.UseCases.Commands;

public sealed record CatchUpHistoryCommand : ICommand<CatchUpReport>;
