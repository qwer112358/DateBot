using CSharpFunctionalExtensions;
using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

/// <summary>Reference to the profile photo in Telegram (MTProto photo id).</summary>
public sealed record PhotoFileId
{
    public const int MaxLength = 256;
    private static readonly PhotoFileIdValidator Validator = new();

    private PhotoFileId(string value) => Value = value;

    public string Value { get; }

    public static Result<PhotoFileId, Error> Create(string value) =>
        Validator.ValidateAndCreate(value.Trim(), "photo_file_id.invalid", v => new PhotoFileId(v));

    public override string ToString() => Value;
}
