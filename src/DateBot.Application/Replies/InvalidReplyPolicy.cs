namespace DateBot.Application.Replies;

/// <summary>What to do when the LLM text fails the checks.</summary>
public enum InvalidReplyPolicy
{
    /// <summary>Send a template instead.</summary>
    Template,

    /// <summary>Put a template draft to Confirm, even in Auto mode.</summary>
    Confirm,
}
