using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches.Validators;

namespace DateBot.Domain.Matches.ValueObjects;

/// <summary>Reference to the profile photo: a Telegram photo id (<c>photo:…</c>) or a file of the dating app (<c>app:…</c>).</summary>
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
