using CSharpFunctionalExtensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DateBot.Application.Abstractions;
using DateBot.Domain.Common;

namespace DateBot.Infrastructure.Persistence;

/// <summary>Boundary helper: database exceptions become <see cref="Result"/> failures.</summary>
internal static class Db
{
    public static async Task<Result<T, Error>> TryAsync<T>(Func<Task<Result<T, Error>>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException or InvalidOperationException)
        {
            return Result.Failure<T, Error>(ServiceErrors.Storage(ex.GetBaseException().Message));
        }
    }

    public static Task<Result<T, Error>> TryAsync<T>(Func<Task<T>> action) =>
        TryAsync(async () => Result.Success<T, Error>(await action()));

    public static async Task<UnitResult<Error>> TryAsync(Func<Task> action)
    {
        var result = await TryAsync(async () =>
        {
            await action();
            return true;
        });
        return result.IsSuccess ? UnitResult.Success<Error>() : UnitResult.Failure(result.Error);
    }
}
