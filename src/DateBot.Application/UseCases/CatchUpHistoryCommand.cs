using DateBot.Application.Abstractions;

namespace DateBot.Application.UseCases;

public sealed record CatchUpHistoryCommand : ICommand<CatchUpReport>;
