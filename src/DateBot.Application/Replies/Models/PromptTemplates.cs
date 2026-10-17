namespace DateBot.Application.Replies.Models;

/// <summary>The sections of the prompts file, placeholders not yet filled.</summary>
/// <param name="Photo">Fills <c>{photo_rule}</c> when the model gets the photo.</param>
/// <param name="NoPhoto">Fills <c>{photo_rule}</c> otherwise.</param>
/// <param name="RevisionSystem">Added to <paramref name="System"/> for «🤖 Переписать».</param>
public sealed record PromptTemplates(
    string System, string Photo, string NoPhoto, string FirstMessage, string RevisionSystem, string Revision);
