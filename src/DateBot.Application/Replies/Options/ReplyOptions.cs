using DateBot.Application.Replies.Enums;

namespace DateBot.Application.Replies.Options;

public sealed class ReplyOptions
{
    public const string Section = "Replies";

    public OwnerProfileOptions Owner { get; init; } = new();

    /// <summary>Used when the profile language cannot be detected.</summary>
    public ReplyLanguage DefaultLanguage { get; init; } = ReplyLanguage.Russian;

    public int MinLength { get; init; } = 20;

    public int MaxLength { get; init; } = 400;

    public int MaxEmoji { get; init; } = 1;

    /// <summary>How many profile photos, main first, the model gets; 0 = none. Notifications show them all anyway.</summary>
    public int MaxPhotos { get; init; } = 10;

    public InvalidReplyPolicy InvalidReplyPolicy { get; init; } = InvalidReplyPolicy.Template;

    /// <summary>The prompts you edit; a relative path is looked up in the project folder (dotnet run), then next to the app.</summary>
    public string PromptsFile { get; init; } = "prompts.md";

    /// <summary>Fallback templates per language (keys: Russian, Uzbek, English). <c>{name}</c> is replaced.</summary>
    public Dictionary<string, string[]> Templates { get; init; } = [];
}
