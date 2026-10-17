using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.UseCases.Models;

namespace DateBot.Application.UseCases.Queries;

public sealed record GetStatusQuery : ICommand<StatusReport>;
