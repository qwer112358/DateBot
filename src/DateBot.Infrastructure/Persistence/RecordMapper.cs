using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Domain.Common;
using DateBot.Domain.Inbox;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;

namespace DateBot.Infrastructure.Persistence;

/// <summary>
/// Domain ↔ table rows. Restoring goes through the same domain factories as new data,
/// so a corrupted row gives an error instead of a broken object.
/// </summary>
internal static class RecordMapper
{
    public static Result<Match, Error> ToDomain(MatchRecord record)
    {
        var errors = new List<Error>();

        var id = Take(MatchId.Create(record.Id), errors);
        var personId = Take(TashdateUserId.Create(record.PersonId), errors);
        var source = Take(Optional(record.BotMessageId, BotMessageId.Create), errors);
        var name = Take(ProfileName.Create(record.Name), errors);
        var age = Take(Optional(record.Age, Age.Create), errors);
        var city = Take(Optional(record.City, City.Create), errors);
        var bio = Take(Optional(record.Bio, Bio.Create), errors);
        var interests = Take(Interests.Create(Lines(record.Interests)), errors);
        var facts = Take(ProfileFacts.Create(Lines(record.Facts)), errors);
        var photo = Take(Optional(record.PhotoFileId, PhotoFileId.Create), errors);
        var username = Take(Optional(record.Username, ContactUsername.Create), errors);
        var appId = Take(Optional(record.AppId, AppProfileId.Create), errors);
        var actions = record.Actions.Select(a => Take(ToState(a), errors)).ToList();

        if (errors.Count > 0)
        {
            return Corrupted($"Profiles/{record.Id}", errors);
        }

        return Profile
            .Create(new ProfileInput(name!, age, city, bio, interests!, photo, username, record.RawText, facts, appId))
            .Bind(profile => Match.Restore(new MatchState(
                id!, personId!, source, record.Kind, profile, record.ReceivedAt, record.Status,
                [.. actions.OfType<MatchActionState>()], record.IsMutual)))
            .MapError(error => ServiceErrors.CorruptedData($"Profiles/{record.Id}: {error.Message}"));
    }

    public static MatchRecord ToRecord(Match match)
    {
        var record = new MatchRecord { Id = match.Id.Value };
        Apply(match, record);
        return record;
    }

    /// <summary>Copies the aggregate onto a tracked row; new actions are added, existing ones updated by sequence.</summary>
    public static void Apply(Match match, MatchRecord record)
    {
        var profile = match.Profile;
        record.PersonId = match.PersonId.Value;
        record.BotMessageId = match.SourceMessageId.HasValue ? match.SourceMessageId.GetValueOrThrow().Value : null;
        record.Name = profile.Name.Value;
        record.Age = profile.Age.HasValue ? profile.Age.GetValueOrThrow().Value : null;
        record.City = profile.City.HasValue ? profile.City.GetValueOrThrow().Value : null;
        record.Bio = profile.Bio.HasValue ? profile.Bio.GetValueOrThrow().Value : null;
        record.Interests = string.Join('\n', profile.Interests.Items);
        record.Facts = string.Join('\n', profile.Facts.Items);
        record.PhotoFileId = profile.Photo.HasValue ? profile.Photo.GetValueOrThrow().Value : null;
        record.Username = profile.Username.HasValue ? profile.Username.GetValueOrThrow().Value : null;
        record.AppId = profile.AppId.HasValue ? profile.AppId.GetValueOrThrow().Value : null;
        record.RawText = profile.RawText;
        record.Kind = match.Kind;
        record.Status = match.Status;
        record.IsMutual = match.IsMutual;
        record.ReceivedAt = match.ReceivedAt;
        record.UpdatedAt = match.Actions.Count == 0 ? match.ReceivedAt : match.Actions.Max(a => a.UpdatedAt);

        foreach (var action in match.Actions)
        {
            var row = record.Actions.FirstOrDefault(a => a.Sequence == action.Sequence);
            if (row is null)
            {
                row = new ActionRecord { ProfileId = record.Id, Sequence = action.Sequence, CreatedAt = action.CreatedAt };
                record.Actions.Add(row);
            }

            row.Type = action.Type;
            row.Status = action.Status;
            row.MessageText = action.Text.HasValue ? action.Text.GetValueOrThrow().Value : null;
            row.Error = action.FailureReason.HasValue ? action.FailureReason.GetValueOrThrow() : null;
            row.UpdatedAt = action.UpdatedAt;
            row.AnchorMessageId = action.Anchor.HasValue ? action.Anchor.GetValueOrThrow().Value : null;
            row.Channel = action.Channel.HasValue ? action.Channel.GetValueOrThrow() : null;
        }
    }

    /// <summary>Tombstone keys of a stored row, the same as <see cref="Match.IdentityKeys"/> of the aggregate.</summary>
    public static IEnumerable<string> IdentityKeys(MatchRecord record) => PersonKeys.Of(record.PersonId, record.Username, record.AppId);

    public static Result<InboxMessage, Error> ToDomain(InboxRecord record) =>
        BotMessageId.Create(record.Id)
            .Bind(id => InboxMessage.Restore(new InboxMessageState(
                id, record.RawText, record.SentAt, record.Status, Maybe.From(record.Note), record.UpdatedAt)))
            .MapError(error => ServiceErrors.CorruptedData($"BotMessages/{record.Id}: {error.Message}"));

    public static InboxRecord ToRecord(InboxMessage message)
    {
        var record = new InboxRecord { Id = message.Id.Value };
        Apply(message, record);
        return record;
    }

    public static void Apply(InboxMessage message, InboxRecord record)
    {
        record.RawText = message.RawText;
        record.SentAt = message.SentAt;
        record.Status = message.Status;
        record.Note = message.Note.HasValue ? message.Note.GetValueOrThrow() : null;
        record.UpdatedAt = message.UpdatedAt;
    }

    private static Result<MatchActionState, Error> ToState(ActionRecord row) =>
        Optional(row.MessageText, ReplyText.Create).Bind<Maybe<ReplyText>, MatchActionState, Error>(text =>
            Optional(row.AnchorMessageId, BotMessageId.Create).Map<Maybe<BotMessageId>, MatchActionState, Error>(anchor =>
                new MatchActionState(
                    row.Sequence, row.Type, row.Status, text, Maybe.From(row.Error), row.CreatedAt, row.UpdatedAt, anchor,
                    row.Channel is { } channel ? channel : Maybe<MessageChannel>.None)));

    private static string[] Lines(string value) => value.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static Result<Maybe<TVo>, Error> Optional<TVo>(string? value, Func<string, Result<TVo, Error>> create) =>
        value is null ? Maybe<TVo>.None : create(value).Map(vo => Maybe.From(vo));

    private static Result<Maybe<TVo>, Error> Optional<TVo>(int? value, Func<int, Result<TVo, Error>> create) =>
        value is null ? Maybe<TVo>.None : create(value.Value).Map(vo => Maybe.From(vo));

    private static Result<Maybe<TVo>, Error> Optional<TVo>(long? value, Func<long, Result<TVo, Error>> create) =>
        value is null ? Maybe<TVo>.None : create(value.Value).Map(vo => Maybe.From(vo));

    private static T? Take<T>(Result<T, Error> result, List<Error> errors)
    {
        if (result.IsFailure)
        {
            errors.Add(result.Error);
            return default;
        }

        return result.Value;
    }

    private static Error Corrupted(string row, IEnumerable<Error> errors) =>
        ServiceErrors.CorruptedData($"{row}: {string.Join("; ", errors.Select(e => e.Message))}");
}
