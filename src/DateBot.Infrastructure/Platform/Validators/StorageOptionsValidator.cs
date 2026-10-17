using DateBot.Infrastructure.Platform.Options;
using FluentValidation;

namespace DateBot.Infrastructure.Platform.Validators;

public sealed class StorageOptionsValidator : AbstractValidator<StorageOptions>
{
    public StorageOptionsValidator()
    {
        RuleFor(o => o.DatabaseFile).NotEmpty();
        RuleFor(o => o.PhotosDirectory).NotEmpty();
    }
}
