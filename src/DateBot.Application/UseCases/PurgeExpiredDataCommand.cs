using DateBot.Application.Abstractions;

namespace DateBot.Application.UseCases;

public sealed record PurgeExpiredDataCommand : ICommand<PurgeReport>;
