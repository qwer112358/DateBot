using CSharpFunctionalExtensions;

namespace DateBot.Domain.Common;

public static class Guard
{
    public static UnitResult<Error> Require(bool condition, Error error) =>
        condition ? UnitResult.Success<Error>() : UnitResult.Failure(error);
}
