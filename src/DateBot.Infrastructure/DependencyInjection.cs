using Anthropic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using DateBot.Application.Ports;
using DateBot.Infrastructure.Llm;
using DateBot.Infrastructure.Notifier;
using DateBot.Infrastructure.Persistence;
using DateBot.Infrastructure.Platform;
using DateBot.Infrastructure.Telegram;

namespace DateBot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TelegramOptions>().Bind(configuration.GetSection(TelegramOptions.Section));
        services.AddOptions<NotifierOptions>().Bind(configuration.GetSection(NotifierOptions.Section));
        services.AddOptions<AnthropicOptions>().Bind(configuration.GetSection(AnthropicOptions.Section));
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.Section));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IJitter, RandomJitter>();
        services.AddSingleton<IPhotoStore, FilePhotoStore>();
        services.AddSingleton<ILoginPrompt, ConsoleLoginPrompt>();

        services.AddDbContext<TashdateDbContext>((sp, db) =>
            db.UseSqlite($"Data Source={sp.GetRequiredService<IOptions<StorageOptions>>().Value.DatabasePath}"));
        services.AddScoped<ChangeSet>();
        services.AddScoped<IMatchRepository, EfMatchRepository>();
        services.AddScoped<IInboxRepository, EfInboxRepository>();
        services.AddScoped<EfSyncStateStore>();
        services.AddScoped<ISyncCursorRepository>(sp => sp.GetRequiredService<EfSyncStateStore>());
        services.AddScoped<IControlStateStore>(sp => sp.GetRequiredService<EfSyncStateStore>());
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IRateLimiter, DbRateLimiter>();
        services.AddScoped<DatabaseInitializer>();

        services.AddSingleton<WTelegramTashdateClient>();
        services.AddSingleton<ITashdateClient>(sp => sp.GetRequiredService<WTelegramTashdateClient>());

        services.AddSingleton<NotifierBot>();
        services.AddSingleton<IOwnerNotifier, TelegramOwnerNotifier>();
        services.AddSingleton<OwnerBotPoller>();

        services.AddSingleton(sp =>
        {
            var anthropic = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
            return new AnthropicClient { ApiKey = anthropic.ApiKey, Timeout = anthropic.Timeout };
        });
        services.AddSingleton<IReplyGenerator, AnthropicReplyGenerator>();

        return services;
    }
}

/// <summary>Creates the data folder and applies EF Core migrations on start.</summary>
public sealed class DatabaseInitializer(TashdateDbContext db, IOptions<StorageOptions> storage)
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
