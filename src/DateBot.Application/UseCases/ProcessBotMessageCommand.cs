using DateBot.Application.Abstractions;
using DateBot.Application.Telegram;

namespace DateBot.Application.UseCases;

public sealed record ProcessBotMessageCommand(RawBotMessage Message) : ICommand<ProcessOutcome>;
