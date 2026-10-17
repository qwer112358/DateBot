using DateBot.Application;
using DateBot.Application.Parsing.Options;
using DateBot.Application.Processing.Options;
using DateBot.Application.Replies.Options;
using DateBot.Infrastructure;
using DateBot.Infrastructure.Llm.Options;
using DateBot.Infrastructure.Llm.Services;
using DateBot.Infrastructure.MiniApp.Options;
using DateBot.Infrastructure.Notifier.Options;
using DateBot.Infrastructure.Persistence;
using DateBot.Infrastructure.Platform.Options;
using DateBot.Infrastructure.Telegram.Options;
using DateBot.Infrastructure.Telegram.Validators;
using DateBot.Worker.Configuration;
using DateBot.Worker.Workers;
using FluentValidation;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);

// Secrets (api_id, api_hash, phone, tokens, API key) live in user-secrets or environment variables, never in the repo.
builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services.AddSerilog((_, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "datebot-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        formatProvider: System.Globalization.CultureInfo.InvariantCulture));

builder.Services.AddValidatorsFromAssemblyContaining<TelegramOptionsValidator>(ServiceLifetime.Singleton);
builder.Services.AddValidatedOptions<ProcessingOptions>(builder.Configuration, ProcessingOptions.Section);
builder.Services.AddValidatedOptions<ParsingOptions>(builder.Configuration, ParsingOptions.Section);
builder.Services.AddValidatedOptions<ReplyOptions>(builder.Configuration, ReplyOptions.Section);
builder.Services.AddValidatedOptions<TelegramOptions>(builder.Configuration, TelegramOptions.Section);
builder.Services.AddValidatedOptions<NotifierOptions>(builder.Configuration, NotifierOptions.Section);
builder.Services.AddValidatedOptions<AnthropicOptions>(builder.Configuration, AnthropicOptions.Section);
builder.Services.AddValidatedOptions<StorageOptions>(builder.Configuration, StorageOptions.Section);
builder.Services.AddValidatedOptions<MiniAppClientOptions>(builder.Configuration, MiniAppClientOptions.Section);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<TashdateWorker>();
builder.Services.AddHostedService<OwnerCommandsWorker>();
builder.Services.AddHostedService<RetentionWorker>();

var host = builder.Build();

await using (var scope = host.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
}

// A broken prompts file stops the start-up, as broken settings do; later edits are checked on every message.
var prompts = host.Services.GetRequiredService<FilePromptTemplates>();
var loaded = await prompts.LoadAsync(CancellationToken.None);
if (loaded.IsFailure)
{
    throw new InvalidOperationException(loaded.Error.Message);
}

host.Services.GetRequiredService<ILogger<Program>>().LogInformation("Prompts are read from {PromptsFile}", prompts.FilePath);

await host.RunAsync();
