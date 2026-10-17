using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches.Validators;

namespace DateBot.Domain.Matches.ValueObjects;

/// <summary>A profile's photos after the main one, for the owner to look through. Invariant: fits one Telegram album with the main photo.</summary>
public sealed record PhotoAlbum
{
    public const int MaxCount = 9;
    private static readonly PhotoAlbumValidator Validator = new();

    private PhotoAlbum(ImmutableArray<PhotoFileId> items) => Items = items;

    public static PhotoAlbum Empty { get; } = new([]);

    public ImmutableArray<PhotoFileId> Items { get; }

    public bool IsEmpty => Items.IsEmpty;

    public static Result<PhotoAlbum, Error> Create(IEnumerable<PhotoFileId> items) =>
        Validator.ValidateAndCreate([.. items.Distinct()], "photo_album.invalid", v => new PhotoAlbum(v));

    public bool Equals(PhotoAlbum? other) => other is not null && Items.SequenceEqual(other.Items);

    public override int GetHashCode() => Items.Aggregate(0, (hash, item) => HashCode.Combine(hash, item));

    public override string ToString() => string.Join("; ", Items);
}
