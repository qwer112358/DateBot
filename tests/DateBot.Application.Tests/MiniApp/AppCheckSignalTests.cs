using DateBot.Application.MiniApp.Enums;
using DateBot.Application.MiniApp.Services;

namespace DateBot.Application.Tests.MiniApp;

public sealed class AppCheckSignalTests
{
    [Fact]
    public async Task Request_wakes_the_waiting_loop()
    {
        var signal = new AppCheckSignal();
        var wait = signal.WaitAsync(CancellationToken.None);
        wait.IsCompleted.Should().BeFalse();

        signal.Request(AppCheckRequest.Wake);

        await wait.WaitAsync(TimeSpan.FromSeconds(5));
        signal.Take().Should().Be(AppCheckRequest.Wake);
    }

    [Fact]
    public void Summary_request_outranks_a_wake_and_take_clears_it()
    {
        var signal = new AppCheckSignal();

        signal.Request(AppCheckRequest.Report);
        signal.Request(AppCheckRequest.Wake);

        signal.Take().Should().Be(AppCheckRequest.Report, "/likes still gets its summary when a card answer comes right after");
        signal.Take().Should().Be(AppCheckRequest.None);
    }

    [Fact]
    public void Taken_request_does_not_wake_the_loop_again()
    {
        var signal = new AppCheckSignal();
        signal.Request(AppCheckRequest.Wake);

        signal.Take();

        signal.WaitAsync(CancellationToken.None).IsCompleted.Should().BeFalse("the loop that already acted on it would spin once for nothing");
    }
}
