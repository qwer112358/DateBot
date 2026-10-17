using CSharpFunctionalExtensions;
using FluentValidation;
using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

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

public sealed class MatchIdValidator : AbstractValidator<Guid>
{
    public MatchIdValidator() =>
        RuleFor(v => v).NotEmpty().OverridePropertyName(nameof(MatchId)).WithErrorCode("match_id.empty");
}
