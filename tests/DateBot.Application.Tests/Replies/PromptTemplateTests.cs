using CSharpFunctionalExtensions;
using DateBot.Application.Notifications;
using DateBot.Application.Replies.Enums;
using DateBot.Application.Replies.Options;
using DateBot.Application.Replies.Services;
using DateBot.Application.Tests.Support;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.States;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Tests.Replies;

/// <summary>The prompts file you edit: its sections, its placeholders, and what a broken edit does.</summary>
public sealed class PromptTemplateTests
{
    [Fact]
    public void Shipped_file_fills_every_placeholder()
    {
        var owner = new OwnerProfileOptions { Name = "Рустам", About = "", Tone = "спокойный", Avoid = "работа" };

        var prompt = PromptBuilder.Build(ShippedPrompts.Templates, AnyProfile(), owner, ReplyLanguage.Uzbek, hasPhoto: true, maxLength: 300);

        prompt.System.Should().Contain("Имя: Рустам").And.Contain("Чем занимается: не указано").And.Contain("не длиннее 300 символов")
            .And.Contain("язык: узбекский").And.Contain("Внешность не обсуждай");
        PromptTemplateParser.PlaceholderPattern().IsMatch(prompt.System + prompt.User).Should().BeFalse();
        prompt.System.Should().NotContain("Подстановки", "the comment above the first section is not sent");
    }

    [Fact]
    public void Edited_text_is_what_the_model_gets()
    {
        var edited = ShippedPrompts.Text.Replace(
            "Напиши первое сообщение этому человеку по правилам выше.", "Напиши коротко и спроси про выходные.", StringComparison.Ordinal);

        var prompt = PromptBuilder.Build(
            PromptTemplateParser.Parse(edited).Value, AnyProfile(), TestOptions.Replies().Owner, ReplyLanguage.Russian, false, 400);

        prompt.User.Should().EndWith("Напиши коротко и спроси про выходные.");
    }

    [Theory]
    [InlineData("## revision", "## revisoin", "нет раздела")]
    [InlineData("{owner_name}", "{owner_nmae}", "{owner_nmae}")]
    [InlineData("Пожелание владельца: {hint}", "Пожелание владельца:", "нет {hint}")]
    public void Broken_edit_is_named_in_the_error(string from, string to, string expected)
    {
        var broken = PromptTemplateParser.Parse(ShippedPrompts.Text.Replace(from, to, StringComparison.Ordinal));

        broken.Error.Message.Should().StartWith("Файл промптов").And.Contain(expected);
    }

    [Fact]
    public async Task Broken_file_gives_a_template_instead_of_stopping_the_message()
    {
        await using var host = new TestHost(processing: TestOptions.TashdateProcessing(), parsing: TestOptions.TashdateParsing());
        host.Prompts.Current = Error.Validation("prompts.invalid", "Файл промптов: нет раздела ## system");
        host.App.AddLike(501, "Лола");

        await host.SyncAppAsync();

        host.Generator.Calls.Should().BeEmpty();
        host.Notifier.Sent.OfType<DraftReadyNotification>().Single().Draft.Value.Should().StartWith("Привет, Лола!");
    }

    private static Profile AnyProfile() =>
        Profile.Create(new ProfileInput(
            ProfileName.Create("Лола").Value,
            Maybe<Age>.None,
            Maybe<City>.None,
            Maybe.From(Bio.Create("Люблю горы").Value),
            Interests.Empty,
            Maybe<PhotoFileId>.None,
            Maybe<ContactUsername>.None,
            "Лола")).Value;
}
