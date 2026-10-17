using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.MiniApp.Services;
using DateBot.Application.Parsing.Options;
using DateBot.Application.Ports.Dating;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.Processing.Options;
using DateBot.Application.Replies.Options;
using DateBot.Application.Telegram.Models;
using DateBot.Application.Tests.Fakes;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.Models;
using DateBot.Application.UseCases.OwnerRequests;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DateBot.Application.Tests.Support;

/// <summary>The real Application container with every port replaced by a fake.</summary>
internal sealed class TestHost : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    public TestHost(ProcessingOptions? processing = null, ReplyOptions? replies = null, ParsingOptions? parsing = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(Options.Create(processing ?? TestOptions.Processing()));
        services.AddSingleton(Options.Create(replies ?? TestOptions.Replies()));
        services.AddSingleton(Options.Create(parsing ?? TestOptions.Parsing()));

        services.AddApplication();

        services.AddSingleton(Store);
        services.AddSingleton<ITashdateClient>(Client);
        services.AddSingleton<ITashdateApp>(App);
        services.AddSingleton<IOwnerNotifier>(Notifier);
        services.AddSingleton<IReplyGenerator>(Generator);
        services.AddSingleton<IPromptTemplates>(Prompts);
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

    public FakeTashdateApp App { get; } = new();

    public FakeNotifier Notifier { get; } = new();

    public FakeReplyGenerator Generator { get; } = new();

    public FakePromptTemplates Prompts { get; } = new();

    public FakeClock Clock { get; } = new();

    public FakeRateLimiter Limiter { get; } = new();

    public FakePhotoStore Photos { get; } = new();

    public ICommandDispatcher Dispatcher => _services.GetRequiredService<ICommandDispatcher>();

    /// <summary>What the main loop would be woken with.</summary>
    public AppCheckSignal AppCheck => _services.GetRequiredService<AppCheckSignal>();

    public Task<Result<ProcessOutcome, Error>> ProcessAsync(RawBotMessage message) =>
        Dispatcher.SendAsync<ProcessBotMessageCommand, ProcessOutcome>(new ProcessBotMessageCommand(message), CancellationToken.None);

    public Task<Result<CatchUpReport, Error>> CatchUpAsync() =>
        Dispatcher.SendAsync<CatchUpHistoryCommand, CatchUpReport>(new CatchUpHistoryCommand(), CancellationToken.None);

    public Task<Result<DraftDecision, Error>> DecideAsync(Guid matchId, bool approve, MessageChannel? channel = null) =>
        Dispatcher.SendAsync<SendReplyCommand, DraftDecision>(new SendReplyCommand(matchId, approve, channel), CancellationToken.None);

    public Task<Result<AppSyncReport, Error>> SyncAppAsync() =>
        Dispatcher.SendAsync<SyncAppLikesCommand, AppSyncReport>(new SyncAppLikesCommand(), CancellationToken.None);

    public Task<Result<Unit, Error>> OwnerAsync(OwnerRequest request) =>
        Dispatcher.SendAsync<HandleOwnerRequestCommand, Unit>(new HandleOwnerRequestCommand(request), CancellationToken.None);

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
