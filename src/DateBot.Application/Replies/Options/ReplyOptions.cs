namespace DateBot.Application.Replies.Options;

/// <summary>
/// Where the message settings live. Everything you tune is in two files read before every message: prompts.md (texts)
/// and llm.json (model, photos, lengths, templates). A relative path is looked up in the project folder under
/// dotnet run, then next to the app.
/// </summary>
public sealed class ReplyOptions
{
    public const string Section = "Replies";

    public OwnerProfileOptions Owner { get; init; } = new();

    public string PromptsFile { get; init; } = "prompts.md";

    public string LlmFile { get; init; } = "llm.json";
}
