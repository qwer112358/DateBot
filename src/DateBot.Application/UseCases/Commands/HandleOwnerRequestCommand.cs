using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.UseCases.OwnerRequests;

namespace DateBot.Application.UseCases.Commands;

public sealed record HandleOwnerRequestCommand(OwnerRequest Request) : ICommand<Unit>;
