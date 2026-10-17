using System.Text.RegularExpressions;
using DateBot.Application.Parsing.Options;
using FluentValidation;

namespace DateBot.Application.Parsing.Validators;

public sealed class ParsingOptionsValidator : AbstractValidator<ParsingOptions>
{
    public ParsingOptionsValidator()
    {
        RuleFor(o => o.LikeMarkers).NotEmpty();
        RuleFor(o => o.MutualMarkers).NotEmpty();
        RuleFor(o => o.ProfilePatterns).NotEmpty().WithMessage("Нужен хотя бы один шаблон анкеты (Parsing:ProfilePatterns)");
        RuleForEach(o => o.ProfilePatterns)
            .Must(p => IsRegex(p) && p.Contains("?<name>", StringComparison.Ordinal))
            .WithMessage("Каждый ProfilePatterns должен быть корректным regex с группой name");
        RuleFor(o => o.InterestsPattern).Must(p => p.Length == 0 || IsRegex(p)).WithMessage("InterestsPattern: некорректный regex");
        RuleFor(o => o.UsernamePattern).Must(p => p.Length == 0 || IsRegex(p)).WithMessage("UsernamePattern: некорректный regex");
        RuleFor(o => o.PersonIdPattern).Must(p => p.Length == 0 || IsRegex(p)).WithMessage("PersonIdPattern: некорректный regex");
        RuleFor(o => o.UnknownMessages).IsInEnum();
    }

    private static bool IsRegex(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
