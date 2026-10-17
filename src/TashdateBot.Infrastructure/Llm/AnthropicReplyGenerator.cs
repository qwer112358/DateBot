using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TashdateBot.Application.Ports;
using TashdateBot.Application.Replies;
using TashdateBot.Application.Telegram;
using DomainError = TashdateBot.Domain.Common.Error;

namespace TashdateBot.Infrastructure.Llm;

/// <summary>
/// Writes the first message with Claude. The profile text and photo go in the user turn as data; the rules
/// are in the system prompt. Any API problem is an error, and the composer then falls back to a template.
/// </summary>
internal sealed class AnthropicReplyGenerator(
    AnthropicClient client,
    IOptions<AnthropicOptions> options,
    ILogger<AnthropicReplyGenerator> logger)
    : IReplyGenerator
{
    public async Task<Result<string, DomainError>> GenerateAsync(ReplyPrompt prompt, Maybe<ProfilePhoto> photo, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return Failure("llm.not_configured", "Ключ Anthropic API не задан, используется шаблон");
        }

        var content = new List<ContentBlockParam>();
        if (photo.HasValue)
        {
            content.Add(new ImageBlockParam
            {
                Source = new Base64ImageSource
                {
                    Data = Convert.ToBase64String(photo.GetValueOrThrow().Content.Span),
                    MediaType = ToMediaType(photo.GetValueOrThrow().MediaType),
                },
            });
        }

        content.Add(new TextBlockParam { Text = prompt.User });

        try
        {
            var response = await client.Messages.Create(
                new MessageCreateParams
                {
                    Model = settings.Model,
                    MaxTokens = settings.MaxTokens,
                    System = prompt.System,
                    OutputConfig = new OutputConfig { Effort = Effort.Low },
                    Messages = [new MessageParam { Role = Role.User, Content = content }],
                },
                ct);

            if (response.StopReason == "refusal")
            {
                return Failure("llm.refused", "Модель отказалась писать сообщение");
            }

            if (response.StopReason == "max_tokens")
            {
                return Failure("llm.truncated", "Ответ модели обрезан по max_tokens");
            }

            var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)).Trim();
            return text.Length == 0 ? Failure("llm.empty", "Модель вернула пустой текст") : Result.Success<string, DomainError>(text);
        }
        catch (AnthropicRateLimitException ex)
        {
            return Logged(ex, "llm.rate_limited", "Лимит запросов Anthropic API");
        }
        catch (Anthropic5xxException ex)
        {
            return Logged(ex, "llm.server_error", "Anthropic API временно недоступен");
        }
        catch (AnthropicApiException ex)
        {
            return Logged(ex, "llm.api_error", $"Anthropic API вернул ошибку: {ex.Message}");
        }
        catch (Exception ex) when (ex is AnthropicException or HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return Logged(ex, "llm.connection", "Нет связи с Anthropic API");
        }
    }

    private static MediaType ToMediaType(string mediaType) => mediaType switch
    {
        "image/png" => MediaType.ImagePng,
        "image/gif" => MediaType.ImageGif,
        "image/webp" => MediaType.ImageWebP,
        _ => MediaType.ImageJpeg,
    };

    private Result<string, DomainError> Logged(Exception ex, string code, string message)
    {
        logger.LogWarning(ex, "Reply generation failed: {Code}", code);
        return Failure(code, message);
    }

    private static Result<string, DomainError> Failure(string code, string message) =>
        Result.Failure<string, DomainError>(DomainError.External(code, message));
}
