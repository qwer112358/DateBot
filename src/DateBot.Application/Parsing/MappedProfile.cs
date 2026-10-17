using DateBot.Domain.Matches;

namespace DateBot.Application.Parsing;

public sealed record MappedProfile(TashdateUserId PersonId, Profile Profile);
