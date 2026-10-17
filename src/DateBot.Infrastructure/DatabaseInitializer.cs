using DateBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DateBot.Infrastructure;

/// <summary>Creates the data folder and applies EF Core migrations on start.</summary>
public sealed class DatabaseInitializer(DateBotDbContext db, IOptions<StorageOptions> storage)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(storage.Value.ResolveDataDirectory());
        Directory.CreateDirectory(storage.Value.PhotosPath);
        await db.Database.MigrateAsync(ct);

        // WAL lets the notifier bot read while the main loop writes.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
    }
}
