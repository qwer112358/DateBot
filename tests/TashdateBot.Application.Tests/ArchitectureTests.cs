using NetArchTest.Rules;
using TashdateBot.Application.UseCases;
using TashdateBot.Domain.Matches;

namespace TashdateBot.Application.Tests;

/// <summary>Dependency rule of the Clean Architecture, checked on the compiled assemblies.</summary>
public sealed class ArchitectureTests
{
    private static readonly string[] Adapters =
    [
        "TashdateBot.Infrastructure", "TashdateBot.Worker", "Microsoft.EntityFrameworkCore", "Microsoft.Data.Sqlite",
        "WTelegram", "TL", "Telegram.Bot", "Anthropic", "System.Net.Http", "Serilog",
    ];

    [Fact]
    public void Domain_depends_on_nothing_but_its_libraries()
    {
        var result = Types.InAssembly(typeof(Match).Assembly)
            .ShouldNot().HaveDependencyOnAny([.. Adapters, "TashdateBot.Application", "Microsoft.Extensions"])
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_references_only_functional_extensions_and_fluent_validation()
    {
        var allowed = new[] { "System", "netstandard", "mscorlib", "CSharpFunctionalExtensions", "FluentValidation" };

        typeof(Match).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => !allowed.Any(prefix => name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal)))
            .Should().BeEmpty();
    }

    [Fact]
    public void Application_does_not_depend_on_adapters()
    {
        var result = Types.InAssembly(typeof(ProcessBotMessageCommand).Assembly)
            .ShouldNot().HaveDependencyOnAny(Adapters)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
