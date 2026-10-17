using FluentValidation;

namespace DateBot.Application.Replies;

public enum ReplyLanguage
{
    Russian,
    Uzbek,
    English,
}

/// <summary>What to do when the LLM text fails the checks.</summary>
public enum InvalidReplyPolicy
{
    /// <summary>Send a template instead.</summary>
    Template,

    /// <summary>Put a template draft to Confirm, even in Auto mode.</summary>
    Confirm,
}

public sealed class ReplyOptions
{
    public const string Section = "Replies";

    public OwnerProfileOptions Owner { get; init; } = new();

    /// <summary>Used when the profile language cannot be detected.</summary>
    public ReplyLanguage DefaultLanguage { get; init; } = ReplyLanguage.Russian;

    public int MinLength { get; init; } = 20;

    public int MaxLength { get; init; } = 400;

    public int MaxEmoji { get; init; } = 1;

    /// <summary>Send the profile photo to the model so the message can mention a neutral detail of it.</summary>
    public bool UsePhoto { get; init; } = true;

    public InvalidReplyPolicy InvalidReplyPolicy { get; init; } = InvalidReplyPolicy.Template;

    /// <summary>Fallback templates per language (keys: Russian, Uzbek, English). <c>{name}</c> is replaced.</summary>
    public Dictionary<string, string[]> Templates { get; init; } = [];
}

/// <summary>Short description of you for the prompt.</summary>
public sealed class OwnerProfileOptions
{
    public string Name { get; init; } = string.Empty;

    public string About { get; init; } = string.Empty;

    public string Tone { get; init; } = string.Empty;

    /// <summary>Topics or phrases the message must not contain.</summary>
    public string Avoid { get; init; } = string.Empty;
}

public sealed class ReplyOptionsValidator : AbstractValidator<ReplyOptions>
{
    public ReplyOptionsValidator()
    {
        RuleFor(o => o.Owner.Name).NotEmpty().WithMessage("Укажите ваше имя для промпта (Replies:Owner:Name)");
        RuleFor(o => o.DefaultLanguage).IsInEnum();
        RuleFor(o => o.MinLength).GreaterThan(0);
        RuleFor(o => o.MaxLength).GreaterThan(o => o.MinLength).LessThanOrEqualTo(1000);
        RuleFor(o => o.MaxEmoji).GreaterThanOrEqualTo(0);
        RuleFor(o => o.InvalidReplyPolicy).IsInEnum();
        RuleFor(o => o.Templates)
            .Must(t => Enum.GetNames<ReplyLanguage>().All(lang => t.TryGetValue(lang, out var list) && list.Length > 0))
            .WithMessage("Нужен хотя бы один шаблон на каждый язык: Russian, Uzbek, English (Replies:Templates)");
    }
}
