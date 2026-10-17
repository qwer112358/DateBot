using CSharpFunctionalExtensions;
using DateBot.Infrastructure.Telegram.Services;

namespace DateBot.Infrastructure.Tests.Telegram;

public sealed class WebAppUrlTests
{
    private static readonly Uri Opened = new(
        "https://dating.example.app/start?lang=ru#tgWebAppData=query_id%3DAAF%26user%3D%257B%2522id%2522%253A42%257D%26auth_date%3D1760700000%26hash%3Dabc"
        + "&tgWebAppVersion=8.0&tgWebAppPlatform=web");

    [Fact]
    public void Init_data_is_the_decoded_fragment_parameter() =>
        WebAppUrl.InitData(Opened).Should().Be(Maybe.From("query_id=AAF&user=%7B%22id%22%3A42%7D&auth_date=1760700000&hash=abc"));

    [Fact]
    public void Api_lives_at_the_origin() =>
        WebAppUrl.Origin(Opened).Should().Be(new Uri("https://dating.example.app/"));

    [Fact]
    public void Url_without_init_data_gives_none() =>
        WebAppUrl.InitData(new Uri("https://dating.example.app/#tgWebAppVersion=8.0")).HasNoValue.Should().BeTrue();
}
