namespace DateBot.Application.Replies;

/// <param name="System">Rules for the model (operator channel).</param>
/// <param name="User">Profile data wrapped in &lt;profile&gt; tags (untrusted).</param>
public sealed record ReplyPrompt(string System, string User, ReplyLanguage Language);
