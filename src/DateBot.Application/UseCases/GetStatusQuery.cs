using DateBot.Application.Abstractions;

namespace DateBot.Application.UseCases;

public sealed record GetStatusQuery : ICommand<StatusReport>;
