using CSharpFunctionalExtensions;
using DateBot.Domain.Inbox;
using DateBot.Domain.Inbox.Enums;
using DateBot.Domain.Shared.ValueObjects;
using DateBot.Domain.Sync;
using static DateBot.Domain.Tests.Support.TestData;

namespace DateBot.Domain.Tests.Sync;

public sealed class SyncAndInboxTests
{
    [Fact]
    public void Cursor_moves_only_forward()
    {
        var cursor = SyncCursor.Start();
        cursor.Position.Should().Be(0);

        cursor.Advance(BotMessageId.Create(10).Value, Now).IsSuccess.Should().BeTrue();
        cursor.Advance(BotMessageId.Create(10).Value, Now).IsFailure.Should().BeTrue();
        cursor.Advance(BotMessageId.Create(9).Value, Now).IsFailure.Should().BeTrue();
        cursor.Advance(BotMessageId.Create(11).Value, Now).IsSuccess.Should().BeTrue();

        cursor.Position.Should().Be(11);
        cursor.IsProcessed(BotMessageId.Create(11).Value).Should().BeTrue();
        cursor.IsProcessed(BotMessageId.Create(12).Value).Should().BeFalse();
    }

    [Fact]
    public void Inbox_message_is_resolved_once()
    {
        var message = InboxMessage.Receive(BotMessageId.Create(5).Value, "текст", Now, Now).Value;

        message.Resolve(InboxStatus.Done, Maybe<string>.None, Now).IsSuccess.Should().BeTrue();
        message.Resolve(InboxStatus.Failed, "again", Now).Error.Code.Should().Be("inbox_message.already_resolved");
        message.Status.Should().Be(InboxStatus.Done);
    }

    [Fact]
    public void Inbox_message_cannot_be_resolved_to_received()
    {
        var message = InboxMessage.Receive(BotMessageId.Create(5).Value, "текст", Now, Now).Value;

        message.Resolve(InboxStatus.Received, Maybe<string>.None, Now).IsFailure.Should().BeTrue();
    }
}
