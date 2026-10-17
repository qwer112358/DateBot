using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Shared.Validators;

namespace DateBot.Domain.Shared.ValueObjects;

/// <summary>Id of a message in the chat with the Tashdate bot (monotonic within the chat).</summary>
public sealed record BotMessageId : IComparable<BotMessageId>
{
    private static readonly BotMessageIdValidator Validator = new();

    private BotMessageId(long value) => Value = value;

    public long Value { get; }

    public static Result<BotMessageId, Error> Create(long value) =>
        Validator.ValidateAndCreate(value, "bot_message_id.invalid", v => new BotMessageId(v));

    public int CompareTo(BotMessageId? other) => other is null ? 1 : Value.CompareTo(other.Value);

    public static bool operator <(BotMessageId left, BotMessageId right) => left.CompareTo(right) < 0;

    public static bool operator >(BotMessageId left, BotMessageId right) => left.CompareTo(right) > 0;

    public static bool operator <=(BotMessageId left, BotMessageId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(BotMessageId left, BotMessageId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
