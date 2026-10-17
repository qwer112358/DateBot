using DateBot.Application.Abstractions;

namespace DateBot.Application.UseCases;

public sealed record HandleOwnerRequestCommand(OwnerRequest Request) : ICommand<Unit>;
