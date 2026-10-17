using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Domain.Common;
using FluentValidation;

namespace DateBot.Domain.Matches;

public sealed record Interests
{
    public const int MaxCount = 30;
    public const int MaxItemLength = 64;
    private static readonly InterestsValidator Validator = new();

    private Interests(ImmutableArray<string> items) => Items = items;

    public static Interests Empty { get; } = new([]);

    public ImmutableArray<string> Items { get; }

    public bool IsEmpty => Items.IsEmpty;

    public static Result<Interests, Error> Create(IEnumerable<string> items) =>
        Validator.ValidateAndCreate(
            items.Select(TextRules.NormalizeSpaces)
                .Where(i => i.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray(),
            "interests.invalid",
            v => new Interests(v));

    public bool Equals(Interests? other) => other is not null && Items.SequenceEqual(other.Items);

    public override int GetHashCode() => Items.Aggregate(0, (hash, item) => HashCode.Combine(hash, item));

    public override string ToString() => string.Join(", ", Items);
}
