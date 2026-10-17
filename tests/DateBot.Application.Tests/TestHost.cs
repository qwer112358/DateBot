using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Application.Replies;
using DateBot.Application.Telegram;
using DateBot.Application.UseCases;
using DateBot.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DateBot.Application.Tests;

/// <summary>The real Application container with every port replaced by a fake.</summary>
internal sealed class TestHost : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    public TestHost(ProcessingOptions? processing = null, ReplyOptions? replies = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(Options.Create(processing ?? TestOptions.Processing()));
        services.AddSingleton(Options.Create(replies ?? TestOptions.Replies()));
        services.AddSingleton(Options.Create(TestOptions.Parsing()));

        services.AddApplication();

        services.AddSingleton(Store);
        services.AddSingleton<ITashdateClient>(Client);
        services.AddSingleton<IOwnerNotifier>(Notifier);
        services.AddSingleton<IReplyGenerator>(Generator);
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IJitter>(new FakeJitter());
        services.AddSingleton<IRateLimiter>(Limiter);
        services.AddSingleton<IPhotoStore>(Photos);
        services.AddScoped<InMemoryRepositories>();
        services.AddScoped<IMatchRepository>(sp => sp.GetRequiredService<InMemoryRepositories>());
        services.AddScoped<IInboxRepository>(sp => sp.GetRequiredService<InMemoryRepositories>());
        services.AddScoped<ISyncCursorRepository>(sp => sp.GetRequiredService<InMemoryRepositories>());
        services.AddScoped<IControlStateStore>(sp => sp.GetRequiredService<InMemoryRepositories>());
        services.AddScoped<IUnitOfWork, FakeUnitOfWork>();

        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public InMemoryStore Store { get; } = new();

    public FakeTashdateClient Client { get; } = new();

    public FakeNotifier Notifier { get; } = new();

    public FakeReplyGenerator Generator { get; } = new();

    public FakeClock Clock { get; } = new();

    public FakeRateLimiter Limiter { get; } = new();

    public FakePhotoStore Photos { get; } = new();

    public ICommandDispatcher Dispatcher => _services.GetRequiredService<ICommandDispatcher>();

    public Task<Result<ProcessOutcome, Error>> ProcessAsync(RawBotMessage message) =>
        Dispatcher.SendAsync<ProcessBotMessageCommand, ProcessOutcome>(new ProcessBotMessageCommand(message), CancellationToken.None);

    public Task<Result<CatchUpReport, Error>> CatchUpAsync() =>
        Dispatcher.SendAsync<CatchUpHistoryCommand, CatchUpReport>(new CatchUpHistoryCommand(), CancellationToken.None);

    public Task<Result<DraftDecision, Error>> DecideAsync(Guid matchId, bool approve) =>
        Dispatcher.SendAsync<SendReplyCommand, DraftDecision>(new SendReplyCommand(matchId, approve), CancellationToken.None);

    public Task<Result<Unit, Error>> OwnerAsync(OwnerRequest request) =>
        Dispatcher.SendAsync<HandleOwnerRequestCommand, Unit>(new HandleOwnerRequestCommand(request), CancellationToken.None);

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
