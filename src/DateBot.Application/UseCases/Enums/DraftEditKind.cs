namespace DateBot.Application.UseCases.Enums;

public enum DraftEditKind
{
    /// <summary>«✏️ Изменить»: your next message is the new text.</summary>
    OwnText,

    /// <summary>«🤖 Переписать»: your next message says what to change, and the model rewrites the draft.</summary>
    Hint,
}
