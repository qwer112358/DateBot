using CSharpFunctionalExtensions;

namespace DateBot.Application.MiniApp.Models;

/// <param name="Mutual">The like made a match: the person had liked us.</param>
/// <param name="Username">Telegram username the app reveals on a match.</param>
public sealed record AppLikeResult(bool Mutual, Maybe<string> Username);
