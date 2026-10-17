using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

/// <summary>
/// Structured profile details beyond the bio, as ready-to-read lines ("Рост: 172 см", "Питомцы: кошка").
/// Platform-neutral: each adapter turns its own fields into these lines.
/// </summary>
public sealed record ProfileFacts
{
    public const int MaxCount = 20;
    public const int MaxItemLength = 120;
    private static readonly ProfileFactsValidator Validator = new();

    private ProfileFacts(ImmutableArray<string> items) => Items = items;

    public static ProfileFacts Empty { get; } = new([]);

    public ImmutableArray<string> Items { get; }

    public bool IsEmpty => Items.IsEmpty;

    public static Result<ProfileFacts, Error> Create(IEnumerable<string> items) =>
        Validator.ValidateAndCreate(
            items.Select(TextRules.NormalizeSpaces)
                .Where(i => i.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray(),
            "profile_facts.invalid",
            v => new ProfileFacts(v));

    public bool Equals(ProfileFacts? other) => other is not null && Items.SequenceEqual(other.Items);

    public override int GetHashCode() => Items.Aggregate(0, (hash, item) => HashCode.Combine(hash, item));

    public override string ToString() => string.Join("; ", Items);
}
