namespace DateBot.Infrastructure.Persistence.Records;

/// <summary>
/// Table <c>ForgottenPeople</c>: the only trace left after a person's data is deleted. The id is the bot id or a
/// profile fingerprint, no personal fields; it keeps the "one auto-message per person" promise after deletion.
/// </summary>
public sealed class ForgottenPersonRecord
{
    public string PersonId { get; set; } = string.Empty;

    public DateTimeOffset ForgottenAt { get; set; }
}
