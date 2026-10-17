using System.Collections.Immutable;

namespace DateBot.Application.MiniApp.Models;

/// <param name="Open">Likes with a visible profile, in the app's order.</param>
/// <param name="Hidden">Likes the app hides until premium: no id, nothing to answer.</param>
public sealed record AppIncomingLikes(ImmutableArray<AppLike> Open, int Hidden);
