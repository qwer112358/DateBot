using Anthropic;
using DateBot.Application.Ports.Dating;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Infrastructure.Llm.Options;
using DateBot.Infrastructure.Llm.Services;
using DateBot.Infrastructure.MiniApp.Options;
using DateBot.Infrastructure.MiniApp.Services;
using DateBot.Infrastructure.Notifier.Options;
using DateBot.Infrastructure.Notifier.Services;
using DateBot.Infrastructure.Persistence;
using DateBot.Infrastructure.Persistence.Repositories;
using DateBot.Infrastructure.Persistence.UnitOfWork;
using DateBot.Infrastructure.Platform.Abstractions;
using DateBot.Infrastructure.Platform.Options;
using DateBot.Infrastructure.Platform.Services;
using DateBot.Infrastructure.Telegram.Abstractions;
using DateBot.Infrastructure.Telegram.Options;
using DateBot.Infrastructure.Telegram.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TelegramOptions>().Bind(configuration.GetSection(TelegramOptions.Section));
        services.AddOptions<NotifierOptions>().Bind(configuration.GetSection(NotifierOptions.Section));
        services.AddOptions<AnthropicOptions>().Bind(configuration.GetSection(AnthropicOptions.Section));
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.Section));
        services.AddOptions<MiniAppClientOptions>().Bind(configuration.GetSection(MiniAppClientOptions.Section));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IJitter, RandomJitter>();
        services.AddSingleton<IPhotoStore, FilePhotoStore>();
        services.AddSingleton<ILoginPrompt, ConsoleLoginPrompt>();

        services.AddDbContext<DateBotDbContext>((sp, db) =>
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
        services.AddSingleton<IWebAppLauncher>(sp => sp.GetRequiredService<WTelegramTashdateClient>());

        services.AddSingleton(sp => new TashdateAppClient(
            new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) })
            {
                Timeout = sp.GetRequiredService<IOptions<MiniAppClientOptions>>().Value.Timeout,
            },
            sp.GetRequiredService<IWebAppLauncher>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<IOptions<MiniAppClientOptions>>(),
            sp.GetRequiredService<ILogger<TashdateAppClient>>()));
        services.AddSingleton<ITashdateApp>(sp => sp.GetRequiredService<TashdateAppClient>());

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
