using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Abstractions.Errors;

/// <summary>Delivery outcomes shared by every channel a message can go through.</summary>
public static class DeliveryErrors
{
    public const string OutcomeUnknownCode = "delivery.outcome_unknown";

    /// <summary>Not retryable: the connection broke after the text was handed over, it may already be delivered.</summary>
    public static Error OutcomeUnknown(string details) => Error.Conflict(
        OutcomeUnknownCode, $"Связь оборвалась во время отправки, неизвестно, дошло ли сообщение: {details}");
}
