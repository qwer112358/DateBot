namespace DateBot.Application.UseCases.OwnerRequests;

/// <summary>What you sent to the notifier bot: a command or a button press.</summary>
public abstract record OwnerRequest(long ChatId, long UserId);
