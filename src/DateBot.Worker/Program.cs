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
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);

// Model settings to experiment with live in llm.json; it sits right after appsettings, so the environment, the command
// line and user-secrets still override it.
var appsettings = builder.Configuration.Sources.OfType<JsonConfigurationSource>().Last(s => s.Path?.StartsWith("appsettings", StringComparison.Ordinal) == true);
builder.Configuration.Sources.Insert(
    builder.Configuration.Sources.IndexOf(appsettings) + 1,
    new JsonConfigurationSource { Path = "llm.json", Optional = false, FileProvider = appsettings.FileProvider });

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

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Prompts are read from {PromptsFile}", prompts.FilePath);
var model = host.Services.GetRequiredService<IOptions<AnthropicOptions>>().Value;
logger.LogInformation(
    "Model {Model}, effort {Effort}, up to {MaxPhotos} photos",
    model.Model,
    model.Effort,
    host.Services.GetRequiredService<IOptions<ReplyOptions>>().Value.MaxPhotos);

await host.RunAsync();
