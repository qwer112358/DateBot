using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches.Validators;

namespace DateBot.Domain.Matches.ValueObjects;

public sealed record MatchId
{
    private static readonly MatchIdValidator Validator = new();

    private MatchId(Guid value) => Value = value;

    public Guid Value { get; }

    public static Result<MatchId, Error> Create(Guid value) =>
        Validator.ValidateAndCreate(value, "match_id.invalid", v => new MatchId(v));

    /// <summary>Short form shown to the owner in notifications.</summary>
    public string Short => Value.ToString("N")[..8];

    public override string ToString() => Value.ToString("N");
}
