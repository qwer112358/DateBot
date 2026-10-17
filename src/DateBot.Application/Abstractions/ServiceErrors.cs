using DateBot.Domain.Common;

namespace DateBot.Application.Abstractions;

public static class ServiceErrors
{
    public static readonly Error Paused = Error.Conflict("service.paused", "Обработка остановлена командой /stop");

    public static readonly Error OwnerNotRegistered = Error.NotFound(
        "service.owner_not_registered", "Бот-уведомитель ещё не знает ваш чат: напишите ему /start");

    public static Error Storage(string details) => Error.Unexpected("storage.failure", details);

    public static Error CorruptedData(string details) => Error.Unexpected("storage.corrupted", details);
}
