using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.UseCases.Models;

namespace DateBot.Application.UseCases.Commands;

public sealed record PurgeExpiredDataCommand : ICommand<PurgeReport>;
