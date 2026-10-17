using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;

namespace DateBot.Domain.Tests;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData(18)]
    [InlineData(35)]
    [InlineData(99)]
    public void Age_accepts_adults(int value) =>
        Age.Create(value).Should().Match<CSharpFunctionalExtensions.Result<Age, Error>>(r => r.IsSuccess && r.Value.Value == value);

    [Theory]
    [InlineData(17)]
    [InlineData(0)]
    [InlineData(100)]
    public void Age_rejects_minors_and_nonsense(int value)
    {
        var result = Age.Create(value);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Details.Should().ContainSingle(d => d.PropertyName == nameof(Age) && d.ErrorCode == "age.out_of_range");
    }

    [Fact]
    public void ProfileName_is_normalized()
    {
        ProfileName.Create("  Анна   Мария ").Value.Value.Should().Be("Анна Мария");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Имя\nс переносом")]
    public void ProfileName_rejects_invalid(string value) => ProfileName.Create(value).IsFailure.Should().BeTrue();

    [Fact]
    public void ProfileName_rejects_too_long() => ProfileName.Create(new string('a', 65)).IsFailure.Should().BeTrue();

    [Theory]
    [InlineData("Привет! Пиши мне https://example.com", "reply_text.link")]
    [InlineData("Мой номер +998 90 123 45 67", "reply_text.phone")]
    [InlineData("Найди меня в t.me/someone", "reply_text.link")]
    [InlineData("Пиши @my_insta_account", "reply_text.mention")]
    public void ReplyText_rejects_contacts(string text, string code)
    {
        var result = ReplyText.Create(text);

        result.IsFailure.Should().BeTrue();
        result.Error.Details.Select(d => d.ErrorCode).Should().Contain(code);
    }

    [Fact]
    public void ReplyText_accepts_plain_message() =>
        ReplyText.Create("Привет! Вижу, ты тоже любишь горы. Где была в последний раз?").IsSuccess.Should().BeTrue();

    [Fact]
    public void ReplyText_collects_all_violations()
    {
        var result = ReplyText.Create("https://x.com и +998901234567");

        result.Error.Details.Select(d => d.ErrorCode).Should().Contain(["reply_text.link", "reply_text.phone"]);
    }

    [Theory]
    [InlineData("@anna_tash", "anna_tash")]
    [InlineData("Anna2000", "Anna2000")]
    public void ContactUsername_strips_at_sign(string input, string expected) =>
        ContactUsername.Create(input).Value.Value.Should().Be(expected);

    [Theory]
    [InlineData("abc")]
    [InlineData("1anna")]
    [InlineData("anna tash")]
    public void ContactUsername_rejects_invalid(string input) => ContactUsername.Create(input).IsFailure.Should().BeTrue();

    [Fact]
    public void Interests_are_trimmed_deduplicated_and_compared_by_value()
    {
        var first = Interests.Create([" горы ", "Книги", "книги", ""]).Value;
        var second = Interests.Create(["горы", "Книги"]).Value;

        first.Items.Should().Equal("горы", "Книги");
        first.Should().Be(second);
    }

    [Fact]
    public void Interests_reject_too_many() =>
        Interests.Create(Enumerable.Range(0, 31).Select(i => $"i{i}")).IsFailure.Should().BeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void BotMessageId_must_be_positive(long value) => BotMessageId.Create(value).IsFailure.Should().BeTrue();

    [Fact]
    public void BotMessageId_is_ordered() =>
        (BotMessageId.Create(5).Value < BotMessageId.Create(6).Value).Should().BeTrue();

    [Fact]
    public void MatchId_rejects_empty_guid() => MatchId.Create(Guid.Empty).IsFailure.Should().BeTrue();

    [Fact]
    public void TashdateUserId_rejects_whitespace() => TashdateUserId.Create("a b").IsFailure.Should().BeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AppProfileId_must_be_positive(long value) => AppProfileId.Create(value).IsFailure.Should().BeTrue();

    [Fact]
    public void ProfileFacts_are_trimmed_and_deduplicated() =>
        ProfileFacts.Create(["  Рост:  172 см ", "рост: 172 см", ""]).Value.Items.Should().Equal("Рост: 172 см");

    [Fact]
    public void ProfileFacts_reject_too_many_and_too_long()
    {
        ProfileFacts.Create(Enumerable.Range(0, ProfileFacts.MaxCount + 1).Select(i => $"f{i}")).IsFailure.Should().BeTrue();
        ProfileFacts.Create([new string('x', ProfileFacts.MaxItemLength + 1)]).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("photo:123")]
    [InlineData("app:a1b2-c3_d4.webp")]
    public void PhotoFileId_accepts_telegram_and_app_photos(string value) => PhotoFileId.Create(value).IsSuccess.Should().BeTrue();

    [Theory]
    [InlineData("app:../x/y.jpg")]
    [InlineData("app:a b.jpg")]
    public void PhotoFileId_rejects_paths_and_spaces(string value) => PhotoFileId.Create(value).IsFailure.Should().BeTrue();
}
