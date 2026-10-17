using FluentValidation;

namespace DateBot.Infrastructure;

public sealed class StorageOptionsValidator : AbstractValidator<StorageOptions>
{
    public StorageOptionsValidator()
    {
        RuleFor(o => o.DatabaseFile).NotEmpty();
        RuleFor(o => o.PhotosDirectory).NotEmpty();
    }
}
