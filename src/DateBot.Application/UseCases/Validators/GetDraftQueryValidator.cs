using DateBot.Application.UseCases.Queries;
using FluentValidation;

namespace DateBot.Application.UseCases.Validators;

public sealed class GetDraftQueryValidator : AbstractValidator<GetDraftQuery>
{
    public GetDraftQueryValidator() => RuleFor(q => q.MatchId).NotEmpty();
}
