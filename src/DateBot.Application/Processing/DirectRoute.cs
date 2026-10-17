using DateBot.Domain.Matches;

namespace DateBot.Application.Processing;

public sealed record DirectRoute(ContactUsername Username) : MessageRoute;
