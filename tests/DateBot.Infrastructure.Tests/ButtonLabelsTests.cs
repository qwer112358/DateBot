using CSharpFunctionalExtensions;
using DateBot.Infrastructure.Telegram;

namespace DateBot.Infrastructure.Tests;

public sealed class ButtonLabelsTests
{
    [Theory]
    [InlineData("❤️", "❤", true)]
    [InlineData("❤️ Лайк", "Лайк", true)]
    [InlineData("Like ❤", "like", true)]
    [InlineData("Дизлайк", "Лайк", false)]
    [InlineData("👎 Dislike", "Like", false)]
    [InlineData("Лайки за неделю", "Лайк", false)]
    [InlineData("💌 Написать", "💌", true)]
    public void Label_matches_only_a_whole_word(string caption, string label, bool expected) =>
        ButtonLabels.Matches(caption, label).Should().Be(expected);

    [Fact]
    public void Labels_are_tried_in_the_configured_order()
    {
        string[] keyboard = ["👎 Дизлайк", "💌", "❤️ Лайк"];

        ButtonLabels.Pick(keyboard, b => b, ["Лайк", "💌"]).Should().Be(Maybe.From("❤️ Лайк"));
        ButtonLabels.Pick(keyboard, b => b, ["Like", "Лайк"]).Should().Be(Maybe.From("❤️ Лайк"));
        ButtonLabels.Pick(keyboard, b => b, ["Yoqdi"]).HasNoValue.Should().BeTrue();
    }
}
