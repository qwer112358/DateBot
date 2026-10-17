namespace DateBot.Domain.Matches.Enums;

/// <summary>How the person reached us.</summary>
public enum MatchKind
{
    /// <summary>The person liked us; scenario A: like back and message.</summary>
    LikedMe = 1,

    /// <summary>Mutual like after our like; scenario B: message only.</summary>
    Mutual = 2,
}
