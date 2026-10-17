using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using DateBot.Application.Ports.Services;
using DateBot.Application.Replies.Models;
using DateBot.Application.Replies.Options;
using DateBot.Application.Replies.Validators;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Infrastructure.Platform.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Infrastructure.Llm.Services;

/// <summary>
/// Reads <see cref="ReplyOptions.LlmFile"/> on every call, so a saved edit applies to the next message. A file broken
/// by an edit (bad JSON, a value out of range) keeps the last good settings working and logs why, once per change;
/// only a file that never loaded is an error, and the start-up refuses that.
/// </summary>
public sealed class FileLlmSettings(IOptions<ReplyOptions> options, IHostEnvironment environment, ILogger<FileLlmSettings> logger)
    : ILlmSettings
{
    private static readonly LlmSettingsValidator Validator = new();

    private static readonly JsonSerializerOptions Json = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Lock _gate = new();
    private Maybe<LlmSettings> _lastGood;
    private string? _lastProblem;

    public string FilePath => ContentFiles.Resolve(environment, options.Value.LlmFile);

    public async Task<Result<LlmSettings, Error>> LoadAsync(CancellationToken ct)
    {
        var path = FilePath;
        var loaded = await ReadAsync(path, ct);
        lock (_gate)
        {
            if (loaded.IsSuccess)
            {
                _lastGood = loaded.Value;
                _lastProblem = null;
                return loaded;
            }

            if (_lastGood.HasNoValue)
            {
                return loaded.Error;
            }

            if (_lastProblem != loaded.Error.Message)
            {
                _lastProblem = loaded.Error.Message;
                logger.LogWarning("{Problem}. The last good settings stay in use until the file is fixed", loaded.Error.Message);
            }

            return _lastGood.GetValueOrThrow();
        }
    }

    private static async Task<Result<LlmSettings, Error>> ReadAsync(string path, CancellationToken ct)
    {
        try
        {
            await using var file = File.OpenRead(path);
            var settings = await JsonSerializer.DeserializeAsync<LlmSettings>(file, Json, ct);
            return settings is null
                ? Result.Failure<LlmSettings, Error>(Invalid(path, "файл пустой"))
                : Validator.Validate(settings).ToResult("llm_settings.invalid").Map(() => settings).MapError(e => Invalid(path, e.Message));
        }
        catch (JsonException ex)
        {
            return Invalid(path, $"не разбирается JSON: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Error.External("llm_settings.unreadable", $"Не читается файл настроек модели {path}: {ex.Message}");
        }
    }

    private static Error Invalid(string path, string reason) =>
        Error.Validation("llm_settings.invalid", $"Файл настроек модели {path}: {reason}");
}
