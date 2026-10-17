using System.Net;
using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Errors;
using DateBot.Application.MiniApp;
using DateBot.Application.MiniApp.Models;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Infrastructure.MiniApp.Options;
using DateBot.Infrastructure.MiniApp.Services;
using DateBot.Infrastructure.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DateBot.Infrastructure.Tests.MiniApp;

/// <summary>The HTTP side of the Mini App against a stub server: sign-in, requests, and how failures are judged.</summary>
public sealed class TashdateAppClientTests
{
    private const long OwnId = 900;
    private static readonly AppProfileId Lola = AppProfileId.Create(501).Value;
    private static readonly ReplyText Text = ReplyText.Create("Привет! Как проходит твой день?").Value;

    private readonly FakeWebAppLauncher _launcher = new();
    private readonly ManualClock _clock = new();

    [Fact]
    public async Task Signs_in_once_and_sends_the_init_data_with_every_request()
    {
        var (client, server) = Create((request, _) => request.RequestUri!.AbsolutePath == "/api/matches/900"
            ? StubHttpHandler.Json("""[{"id": 501, "name": "Лола", "username": "lola_tash"}]""")
            : null);

        var first = await client.GetMatchesAsync(default);
        await client.GetMatchesAsync(default);

        first.Value.Should().ContainSingle().Which.Username.Should().Be(Maybe.From("lola_tash"));
        _launcher.Opened.Should().Be(1);
        server.Requests.Select(r => r.PathAndQuery).Should().Equal("/api/user/42", "/api/i18n/ru", "/api/matches/900", "/api/matches/900");
        server.Requests.Should().OnlyContain(r => r.InitData == "init-1");
    }

    [Fact]
    public async Task Old_sign_in_is_renewed()
    {
        var (client, _) = Create((request, _) => StubHttpHandler.Json("[]"));

        await client.GetMatchesAsync(default);
        _clock.UtcNow += TimeSpan.FromHours(1);
        await client.GetMatchesAsync(default);

        _launcher.Opened.Should().Be(2);
    }

    [Fact]
    public async Task Rejected_sign_in_is_renewed_once_and_the_request_repeated()
    {
        var (client, server) = Create((request, _) =>
            request.Headers.GetValues(TashdateAppClient.InitDataHeader).Single() == "init-1" && request.RequestUri!.AbsolutePath == "/api/matches/900"
                ? StubHttpHandler.Json("""{"detail": "Invalid init data"}""", HttpStatusCode.Unauthorized)
                : StubHttpHandler.Json("[]"));

        var result = await client.GetMatchesAsync(default);

        result.IsSuccess.Should().BeTrue();
        _launcher.Opened.Should().Be(2);
        server.Requests.Last().InitData.Should().Be("init-2");
    }

    [Fact]
    public async Task Like_posts_both_profile_ids_and_reads_the_match()
    {
        var (client, server) = Create((request, _) => request.RequestUri!.AbsolutePath == "/api/like"
            ? StubHttpHandler.Json("""{"match": true, "username": "lola_tash"}""")
            : null);

        var like = await client.LikeAsync(Lola, default);

        like.Value.Should().Be(new AppLikeResult(true, Maybe.From("lola_tash")));
        server.Requests.Single(r => r.PathAndQuery == "/api/like").Body.Should().Be("""{"from_user":900,"to_user":501}""");
    }

    [Fact]
    public async Task Daily_like_limit_is_a_pause_not_a_failure()
    {
        var (client, _) = Create((request, _) => request.RequestUri!.AbsolutePath == "/api/like"
            ? StubHttpHandler.Json("""{"error": "like_limit_reached", "limit": 30, "verified": false, "verify_bonus": 20}""", HttpStatusCode.TooManyRequests)
            : null);

        var like = await client.LikeAsync(Lola, default);

        like.Error.Code.Should().Be(AppErrors.LikeLimitCode);
        like.Error.Type.Should().Be(ErrorType.RateLimited);
        like.Error.Message.Should().Contain("30");
    }

    [Fact]
    public async Task Locked_own_profile_pauses_instead_of_failing_every_like()
    {
        var (client, _) = Create((request, _) => request.RequestUri!.AbsolutePath == "/api/like"
            ? StubHttpHandler.Json("""{"detail": {"code": "profile_not_approved"}}""", HttpStatusCode.Forbidden)
            : null);

        var like = await client.LikeAsync(Lola, default);

        ErrorClassifier.IsRetryable(like.Error).Should().BeTrue();
        like.Error.Message.Should().Contain("profile_not_approved");
        _launcher.Opened.Should().Be(1, "a known 403 code is not a sign-in problem");
    }

    [Fact]
    public async Task Chat_message_carries_the_client_key()
    {
        var (client, server) = Create((request, _) => request.RequestUri!.AbsolutePath == "/api/chat/send"
            ? StubHttpHandler.Json("""{"id": 77}""")
            : null);

        var sent = await client.SendMessageAsync(Lola, Text, "key-1", default);

        sent.IsSuccess.Should().BeTrue();
        server.Requests.Single(r => r.PathAndQuery == "/api/chat/send").Body.Should()
            .Contain("\"from_user\":900").And.Contain("\"to_user\":501").And.Contain("\"client_key\":\"key-1\"").And.Contain("\"reply_to_id\":null");
    }

    [Fact]
    public async Task Closed_chat_is_a_refusal()
    {
        var (client, _) = Create((request, _) => request.RequestUri!.AbsolutePath == "/api/chat/send"
            ? StubHttpHandler.Json("""{"detail": {"code": "chat_unavailable"}}""", HttpStatusCode.Forbidden)
            : null);

        (await client.SendMessageAsync(Lola, Text, "key-1", default)).Error.Should().Be(AppErrors.ChatUnavailable);
    }

    [Fact]
    public async Task Server_error_on_a_chat_message_may_hide_a_delivered_message()
    {
        var (client, _) = Create((request, _) => request.RequestUri!.AbsolutePath is "/api/chat/send" or "/api/like"
            ? StubHttpHandler.Json("""{"detail": "boom"}""", HttpStatusCode.BadGateway)
            : null);

        (await client.SendMessageAsync(Lola, Text, "key-1", default)).Error.Code.Should().Be(DeliveryErrors.OutcomeUnknownCode);
        ErrorClassifier.IsRetryable((await client.LikeAsync(Lola, default)).Error).Should().BeTrue("a repeated like is harmless");
    }

    [Fact]
    public async Task Failure_to_connect_proves_the_message_was_not_sent()
    {
        var (client, _) = Create((request, _) => request.RequestUri!.AbsolutePath == "/api/chat/send"
            ? throw new HttpRequestException(HttpRequestError.ConnectionError, "connection refused")
            : null);

        var sent = await client.SendMessageAsync(Lola, Text, "key-1", default);

        sent.Error.Code.Should().Be("miniapp.unavailable");
    }

    [Fact]
    public async Task Likes_are_read_page_by_page_and_hidden_ones_counted()
    {
        var (client, server) = Create((request, _) => request.RequestUri!.PathAndQuery switch
        {
            "/api/likes/900?limit=30&offset=0" => StubHttpHandler.Json(
                """[{"id": 501, "name": "Лола", "locked": false}, {"id": null, "locked": true}]""",
                HttpStatusCode.OK,
                ("X-Likes-Has-More", "1"), ("X-Likes-Offset-Next", "2"), ("X-Likes-Gift-Consumed", "0")),
            "/api/likes/900?limit=30&offset=2&gc=0" => StubHttpHandler.Json(
                """[{"id": 502, "name": "Мадина", "locked": false}]""", HttpStatusCode.OK, ("X-Likes-Has-More", "0")),
            _ => null,
        });

        var likes = await client.GetIncomingLikesAsync(100, default);

        likes.Value.Open.Select(l => l.ProfileId).Should().Equal(501, 502);
        likes.Value.Hidden.Should().Be(1);
        server.Requests.Count(r => r.PathAndQuery.StartsWith("/api/likes/", StringComparison.Ordinal)).Should().Be(2);
    }

    [Fact]
    public async Task Profile_labels_come_from_the_app_dictionary()
    {
        var (client, _) = Create((request, _) => request.RequestUri!.AbsolutePath == "/api/user/id/501"
            ? StubHttpHandler.Json("""{"id": 501, "name": "Лола", "interests": "[\"coffee\"]", "photos": "[\"p1.jpg\"]"}""")
            : null);

        var profile = await client.GetProfileAsync(Lola, default);

        profile.Value.Interests.Should().Equal("Кофе");
        profile.Value.PhotoFiles.Should().Equal("p1.jpg");
    }

    [Fact]
    public async Task Missing_profile_is_not_found()
    {
        var (client, _) = Create((request, _) => request.RequestUri!.AbsolutePath == "/api/user/id/501"
            ? StubHttpHandler.Json("""{"detail": "not found"}""", HttpStatusCode.NotFound)
            : null);

        (await client.GetProfileAsync(Lola, default)).Error.Code.Should().Be("miniapp.profile_not_found");
    }

    [Fact]
    public async Task Photo_is_downloaded_by_its_app_file_name()
    {
        var (client, server) = Create((request, _) => request.RequestUri!.AbsolutePath == "/photos/p1.jpg"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0xFF, 0xD8, 0xFF]) }
            : null);

        var photo = await client.DownloadPhotoAsync(PhotoFileId.Create("app:p1.jpg").Value, default);

        photo.Value.GetValueOrThrow().MediaType.Should().Be("image/jpeg");
        server.Requests.Last().PathAndQuery.Should().Be("/photos/p1.jpg");
    }

    private (TashdateAppClient Client, StubHttpHandler Server) Create(Func<HttpRequestMessage, string, HttpResponseMessage?> app)
    {
        var server = new StubHttpHandler((request, body) => request.RequestUri!.AbsolutePath switch
        {
            "/api/user/42" => StubHttpHandler.Json($$"""{"id": {{OwnId}}, "name": "Рустам"}"""),
            "/api/i18n/ru" => StubHttpHandler.Json("""{"lang": "ru", "strings": {"interest.coffee": "Кофе"}}"""),
            _ => app(request, body) ?? StubHttpHandler.Json("""{"detail": "unexpected"}""", HttpStatusCode.NotFound),
        });
        var client = new TashdateAppClient(
            new HttpClient(server),
            _launcher,
            _clock,
            Options.Create(new MiniAppClientOptions()),
            NullLogger<TashdateAppClient>.Instance);
        return (client, server);
    }
}
