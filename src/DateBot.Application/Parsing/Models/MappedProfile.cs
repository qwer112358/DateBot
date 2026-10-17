using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Parsing.Models;

public sealed record MappedProfile(TashdateUserId PersonId, Profile Profile);
