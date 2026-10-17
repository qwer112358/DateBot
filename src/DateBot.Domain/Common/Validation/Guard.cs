using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;

namespace DateBot.Domain.Common.Validation;

public static class Guard
{
    public static UnitResult<Error> Require(bool condition, Error error) =>
        condition ? UnitResult.Success<Error>() : UnitResult.Failure(error);
}
