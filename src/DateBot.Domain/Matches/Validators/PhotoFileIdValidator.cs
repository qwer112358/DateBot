using DateBot.Domain.Matches.ValueObjects;
using FluentValidation;

namespace DateBot.Domain.Matches.Validators;

public sealed class PhotoFileIdValidator : AbstractValidator<string>
{
    public PhotoFileIdValidator() =>
        RuleFor(v => v)
            .NotEmpty().WithErrorCode("photo_file_id.empty")
            .MaximumLength(PhotoFileId.MaxLength).WithErrorCode("photo_file_id.too_long")
            .Matches(@"^[A-Za-z0-9:_.\-]+$").WithErrorCode("photo_file_id.format")
            .OverridePropertyName(nameof(PhotoFileId));
}
