using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using CSharpFunctionalExtensions;
using DateBot.Application.MiniApp;
using DateBot.Application.MiniApp.Models;
using DateBot.Application.Parsing.Services;
using DateBot.Application.Ports.Dating;
using DateBot.Application.Ports.Services;
using DateBot.Application.Telegram.Models;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Infrastructure.MiniApp.Enums;
using DateBot.Infrastructure.MiniApp.Models;
using DateBot.Infrastructure.MiniApp.Options;
using DateBot.Infrastructure.MiniApp.Readers;
using DateBot.Infrastructure.Platform.Services;
using DateBot.Infrastructure.Telegram.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Infrastructure.MiniApp.Services;

/// <summary>
/// The Tashdate Mini App API (<c>/api/…</c>) used as your account. Every request carries the init data Telegram signs
/// for the userbot (<c>X-Telegram-Init-Data</c>); a rejected sign-in is renewed once and the request repeated.
/// HTTP and network failures never leave this class as exceptions.
/// </summary>
internal sealed class TashdateAppClient(
    HttpClient http,
    IWebAppLauncher launcher,
    IClock clock,
    IOptions<MiniAppClientOptions> options,
    ILogger<TashdateAppClient> logger)
    : ITashdateApp, IDisposable
{
    public const string InitDataHeader = "X-Telegram-Init-Data";
    private const int LikesPageSize = 30;
    private static readonly TimeSpan LikeLimitRetry = TimeSpan.FromHours(1);
    private static readonly string[] KeptHeaders = ["X-Likes-Has-More", "X-Likes-Offset-Next", "X-Likes-Gift-Consumed", "X-Likes-Total", "Retry-After"];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private AppSession? _session;

    public Task<Result<AppIncomingLikes, Error>> GetIncomingLikesAsync(int max, CancellationToken ct) =>
        WithSessionAsync(session => ReadLikesAsync(session, max, ct), ct);

    public Task<Result<AppProfile, Error>> GetProfileAsync(AppProfileId id, CancellationToken ct) =>
        WithSessionAsync(
            session => SendAsync(session, HttpMethod.Get, $"api/user/id/{id}", null, AppRequestKind.Read, ct)
                .Bind(response => response.Status == HttpStatusCode.NotFound
                    ? Result.Failure<AppProfile, Error>(AppErrors.ProfileNotFound(id))
                    : Expect(response, AppRequestKind.Read).Bind(ok => AppResponses.Profile(ok.Json(), session.Translations))),
            ct);

    public Task<Result<ImmutableArray<AppMatch>, Error>> GetMatchesAsync(CancellationToken ct) =>
        WithSessionAsync(
            session => SendAsync(session, HttpMethod.Get, $"api/matches/{session.OwnId}", null, AppRequestKind.Read, ct)
                .Bind(response => Expect(response, AppRequestKind.Read))
                .Map(ok => AppResponses.Matches(ok.Json())),
            ct);

    public Task<Result<AppLikeResult, Error>> LikeAsync(AppProfileId id, CancellationToken ct) =>
        WithSessionAsync(
            session => SendAsync(session, HttpMethod.Post, "api/like", new { from_user = session.OwnId, to_user = id.Value }, AppRequestKind.Like, ct)
                .Bind(response => Expect(response, AppRequestKind.Like))
                .Map(ok => AppResponses.LikeResult(ok.Json())),
            ct);

    public Task<UnitResult<Error>> SendMessageAsync(AppProfileId to, ReplyText text, string clientKey, CancellationToken ct) =>
        WithSessionAsync(
            session => SendAsync(
                    session,
                    HttpMethod.Post,
                    "api/chat/send",
                    new { from_user = session.OwnId, to_user = to.Value, text = text.Value, client_key = clientKey, reply_to_id = (long?)null },
                    AppRequestKind.ChatMessage,
                    ct)
                .Bind(response => Expect(response, AppRequestKind.ChatMessage))
                .Map(_ => true),
            ct).Bind(_ => UnitResult.Success<Error>());

    public Task<Result<Maybe<ProfilePhoto>, Error>> DownloadPhotoAsync(PhotoFileId photo, CancellationToken ct) =>
        WithSessionAsync(
            session => SendAsync(session, HttpMethod.Get, "photos/" + Uri.EscapeDataString(ProfileMapper.AppPhotoFile(photo)), null, AppRequestKind.Read, ct)
                .Bind(response => response.Status == HttpStatusCode.NotFound || response.Body.Length == 0
                    ? Result.Success<Maybe<ProfilePhoto>, Error>(Maybe<ProfilePhoto>.None)
                    : Expect(response, AppRequestKind.Read).Map(ok => Maybe.From(new ProfilePhoto(
                        ok.Body, ok.MediaType is { } type && type.StartsWith("image/", StringComparison.Ordinal) ? type : FilePhotoStore.DetectMediaType(ok.Body))))),
            ct);

    public void Dispose()
    {
        _gate.Dispose();
        http.Dispose();
    }

    private async Task<Result<AppIncomingLikes, Error>> ReadLikesAsync(AppSession session, int max, CancellationToken ct)
    {
        var open = ImmutableArray.CreateBuilder<AppLike>();
        var hidden = 0;
        var offset = 0;
        var giftsConsumed = Maybe<string>.None;
        while (open.Count + hidden < max)
        {
            var query = $"api/likes/{session.OwnId}?limit={LikesPageSize}&offset={offset}"
                + giftsConsumed.Map(gc => "&gc=" + Uri.EscapeDataString(gc)).GetValueOrDefault(string.Empty);
            var page = await SendAsync(session, HttpMethod.Get, query, null, AppRequestKind.Read, ct)
                .Bind(response => Expect(response, AppRequestKind.Read));
            if (page.IsFailure)
            {
                return page.Error;
            }

            var (pageOpen, pageHidden) = AppResponses.Likes(page.Value.Json());
            open.AddRange(pageOpen.Where(like => open.All(seen => seen.ProfileId != like.ProfileId)));
            hidden += pageHidden;

            var count = pageOpen.Length + pageHidden;
            if (count == 0 || page.Value.Header("X-Likes-Has-More").GetValueOrDefault("0") != "1")
            {
                break;
            }

            offset = page.Value.Header("X-Likes-Offset-Next").Bind(ParseInt).GetValueOrDefault(offset + count);
            giftsConsumed = page.Value.Header("X-Likes-Gift-Consumed");
        }

        return new AppIncomingLikes(open.ToImmutable(), hidden);
    }

    /// <summary>Runs <paramref name="action"/> with a signed-in session; a rejected sign-in is renewed and tried once more.</summary>
    private async Task<Result<T, Error>> WithSessionAsync<T>(Func<AppSession, Task<Result<T, Error>>> action, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var session = await SessionAsync(renew: attempt > 1, ct);
            if (session.IsFailure)
            {
                return session.Error;
            }

            var result = await action(session.Value);
            if (result.IsSuccess || result.Error.Code != AppErrors.AuthRejectedCode || attempt > 1)
            {
                return result;
            }

            logger.LogInformation("The app rejected the Telegram sign-in; signing in again");
        }
    }

    private async Task<Result<AppSession, Error>> SessionAsync(bool renew, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!renew && _session is { } current && clock.UtcNow - current.OpenedAt < options.Value.SessionLifetime)
            {
                return current;
            }

            var opened = await OpenSessionAsync(ct);
            _session = opened.IsSuccess ? opened.Value : null;
            return opened;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Result<AppSession, Error>> OpenSessionAsync(CancellationToken ct)
    {
        var configured = options.Value.Url.Length > 0 ? Maybe.From(new Uri(options.Value.Url)) : Maybe<Uri>.None;
        var webApp = await launcher.OpenWebAppAsync(configured, ct);
        if (webApp.IsFailure)
        {
            return webApp.Error;
        }

        var signIn = new AppSession(webApp.Value, 0, AppTranslations.Empty, clock.UtcNow);
        var own = await SendAsync(signIn, HttpMethod.Get, $"api/user/{webApp.Value.TelegramUserId}", null, AppRequestKind.Read, ct)
            .Bind(response => Expect(response, AppRequestKind.Read))
            .Bind(ok => AppResponses.OwnId(ok.Json()));
        if (own.IsFailure)
        {
            return own.Error;
        }

        var labels = await SendAsync(signIn, HttpMethod.Get, $"api/i18n/{options.Value.Language}", null, AppRequestKind.Read, ct)
            .Bind(response => Expect(response, AppRequestKind.Read))
            .Map(ok => AppTranslations.From(ok.Json()));
        if (labels.IsFailure)
        {
            logger.LogWarning("App labels unavailable, codes will be shown as is: {Error}", labels.Error);
        }

        logger.LogInformation("Signed in to the Mini App at {Address} as profile {OwnId}", webApp.Value.BaseAddress, own.Value);
        return signIn with { OwnId = own.Value, Translations = labels.GetValueOrDefault(AppTranslations.Empty) };
    }

    private async Task<Result<AppResponse, Error>> SendAsync(
        AppSession session, HttpMethod method, string path, object? json, AppRequestKind kind, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, new Uri(session.WebApp.BaseAddress, path));
        request.Headers.Add(InitDataHeader, session.WebApp.InitData);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (options.Value.UserAgent.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", options.Value.UserAgent);
        }

        if (json is not null)
        {
            request.Content = JsonContent.Create(json);
        }

        try
        {
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsByteArrayAsync(ct);
            var headers = KeptHeaders
                .Where(name => response.Headers.Contains(name))
                .ToDictionary(name => name, name => response.Headers.GetValues(name).First(), StringComparer.OrdinalIgnoreCase);
            return new AppResponse(response.StatusCode, body, response.Content.Headers.ContentType?.MediaType, headers);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or SocketException or OperationCanceledException)
        {
            return kind == AppRequestKind.ChatMessage && MaybeDelivered(ex)
                ? AppErrors.SendOutcomeUnknown(ex.Message)
                : AppErrors.Unavailable(ex.Message);
        }
    }

    /// <summary>Only a failure to connect proves the request never reached the app.</summary>
    private static bool MaybeDelivered(Exception ex) =>
        ex is not HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.SecureConnectionError };

    private static Result<AppResponse, Error> Expect(AppResponse response, AppRequestKind kind) =>
        response.IsSuccess ? response : ToError(response, kind);

    private static Error ToError(AppResponse response, AppRequestKind kind)
    {
        var body = response.Json();
        var code = AppResponses.ErrorCode(body);
        var status = (int)response.Status;
        return status switch
        {
            401 => AppErrors.AuthRejected(status),
            403 or 404 when code.Where(c => c.StartsWith("profile_", StringComparison.Ordinal)).HasValue =>
                AppErrors.AccountLocked(code.GetValueOrThrow()),
            403 when code.Where(c => c == "chat_unavailable").HasValue => AppErrors.ChatUnavailable,
            403 when code.HasNoValue => AppErrors.AuthRejected(status),
            429 when code.Where(c => c == "like_limit_reached").HasValue =>
                AppErrors.LikeLimit((int)AppJson.Long(body, "limit").GetValueOrDefault(30), LikeLimitRetry),
            429 => AppErrors.TooManyRequests(response.Header("Retry-After").Bind(ParseInt).Map(s => TimeSpan.FromSeconds(s)).GetValueOrDefault(TimeSpan.FromMinutes(5))),
            >= 500 when kind == AppRequestKind.ChatMessage => AppErrors.SendOutcomeUnknown($"HTTP {status}"),
            >= 500 => AppErrors.Unavailable($"HTTP {status}"),
            _ => AppErrors.Refused(status, code.GetValueOrDefault(Snippet(response.Body))),
        };
    }

    private static string Snippet(byte[] body) =>
        body.Length == 0 ? "пустой ответ" : System.Text.Encoding.UTF8.GetString(body, 0, Math.Min(body.Length, 200));

    private static Maybe<int> ParseInt(string value) =>
        int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number)
            ? number
            : Maybe<int>.None;
}
