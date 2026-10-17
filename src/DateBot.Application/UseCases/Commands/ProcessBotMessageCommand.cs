using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.Telegram.Models;
using DateBot.Application.UseCases.Enums;

namespace DateBot.Application.UseCases.Commands;

public sealed record ProcessBotMessageCommand(RawBotMessage Message) : ICommand<ProcessOutcome>;
