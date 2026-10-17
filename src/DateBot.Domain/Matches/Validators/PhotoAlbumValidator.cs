using System.Collections.Immutable;
using DateBot.Domain.Matches.ValueObjects;
using FluentValidation;

namespace DateBot.Domain.Matches.Validators;

public sealed class PhotoAlbumValidator : AbstractValidator<ImmutableArray<PhotoFileId>>
{
    public PhotoAlbumValidator() =>
        RuleFor(v => v.Length)
            .LessThanOrEqualTo(PhotoAlbum.MaxCount).WithErrorCode("photo_album.too_many")
            .OverridePropertyName(nameof(PhotoAlbum));
}
